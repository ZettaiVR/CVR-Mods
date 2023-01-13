using ABI.CCK.Components;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using ABI_RC.Core.Networking.API.Responses;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Scripting;
using System.Linq;
using UnityEngine;
using Zettai;

[assembly: MelonInfo(typeof(MemoryCache), "MemoryCache", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]

namespace Zettai
{
    public class MemoryCache : MelonMod
    {
        private static MelonPreferences_Entry<bool> enableMod;
        internal static MelonPreferences_Entry<bool> enableLog;
        internal static MelonPreferences_Entry<bool> enableHologram;
        internal static MelonPreferences_Entry<bool> enableOwnSanitizer;
        internal static MelonPreferences_Entry<bool> enableGC;
        private static MelonPreferences_Entry<byte> maxLifeTimeMinutesEntry;
        private static MelonPreferences_Entry<byte> maxRemoveCountEntry;
        private static MelonPreferences_Entry<byte> loadTimeoutSecondsEntry;
        private static bool FlushCache = false;
        private static readonly System.Threading.SemaphoreSlim bundleLoadSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private static readonly System.Threading.SemaphoreSlim instantiateSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private static readonly Dictionary<CacheKey, CacheItem> cachedAssets = new Dictionary<CacheKey, CacheItem>();
        private static readonly Dictionary<CacheKey, TimeSpan> idsToRemove = new Dictionary<CacheKey, TimeSpan>(10);
        private readonly static Dictionary<ulong, string> fileIds = new Dictionary<ulong, string>();
        private static readonly HashSet<CacheKey> idsToRemoveStrings = new HashSet<CacheKey>(); 
        private static readonly Guid BLOCKED_GUID = new Guid("B10CED00-F372-4ECE-B362-1F48E64D2F7E");
        private static readonly CacheKey BlockedKey = new CacheKey(BLOCKED_GUID, 0, 0);
        const string _BLOCKED_NAME = "Blocked";
        const string _BLOCKED_VERSION = "000000000000";
        const string _PROP_PATH = "assets/abi.cck/resources/cache/_cvrspawnable.prefab";
        const string _AVATAR_PATH = "assets/abi.cck/resources/cache/_cvravatar.prefab";
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            enableMod = category.CreateEntry("enableMemoryCacheMod", true, "Enable MemoryCache mod");
            enableLog = category.CreateEntry("enableMemoryCacheLog", false, "Enable MemoryCache logging");
            enableGC = category.CreateEntry("enableGC", false, "Enable GC after Instantiation");
            enableOwnSanitizer = category.CreateEntry("enableOwnSanitizer", false, "Enable MemoryCache's custom asset cleaning/sanitizer");
            enableHologram = category.CreateEntry("enableHologram", true, "Enable Hologram");
            maxLifeTimeMinutesEntry = category.CreateEntry("maxLifeTimeMinutesEntry", (byte)15, "Maximum lifetime of unused assets in cache (minutes)");
            maxRemoveCountEntry = category.CreateEntry("maxRemoveCountEntry", (byte)5, "Maximum number of assets to remove from cache per minute");
            loadTimeoutSecondsEntry = category.CreateEntry("loadTimeoutSecondsEntry", (byte)30, "Maximum time in seconds for assets to wait for loading before giving up");
            enableMod.OnValueChanged += EnableMod_OnValueChanged;
        }
        private void EnableMod_OnValueChanged(bool arg1, bool arg2) => EmptyCache();
        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => UpdateBlockedAvatar();
        public override void OnApplicationLateStart()
        {
            UpdateBlockedAvatar();
            SchedulerSystem.AddJob(new SchedulerSystem.Job(CleanupCache), 5f, 60f, -1);
        }
        private static void UpdateBlockedAvatar() 
        {
            try
            {
                cachedAssets.Remove(BlockedKey);
                cachedAssets[BlockedKey] = new CacheItem(_BLOCKED_NAME, _BLOCKED_VERSION, DownloadTask.ObjectType.Avatar, MetaPort.Instance?.blockedAvatarPrefab, new Tags(), _BLOCKED_NAME, true);
            }
            catch (Exception e)
            {
                MelonLogger.Msg(e.Message + e.StackTrace, e);
            }
        }
        public static void CleanupCache()
        {
            idsToRemove.Clear();
            var keys = cachedAssets.Keys;
            var now = DateTime.UtcNow;
            byte maxLifeTimeMinutes = FlushCache ? (byte)0 : maxLifeTimeMinutesEntry.Value;
            var maxAge = TimeSpan.FromMinutes(maxLifeTimeMinutes);
            foreach (var id in keys)
            {
                var item = cachedAssets[id];
                if (item.CanRemove(maxAge, now, out var age))
                    idsToRemove.Add(id, age);
            }
            if (idsToRemove.Count == 0)
                return;
            byte maxRemoveCount = FlushCache ? (byte)0 : maxRemoveCountEntry.Value;
            LimitRemovedCount(maxRemoveCount);
            if (enableLog.Value)
            {
                string removedNumber = maxRemoveCount == 0 ? "no limit on the number of assets" : $"maximum number of assets to remove was {maxRemoveCount}";
                MelonLogger.Msg($"Clearing cache, removing {idsToRemove.Count} cached items. Maximum lifetime was {maxLifeTimeMinutesEntry.Value} minutes, {removedNumber}.");
            }
            foreach (var item in idsToRemove)
            {
                cachedAssets[item.Key].Destroy();
                cachedAssets.Remove(item.Key);
                fileIds.Remove(item.Key.KeyHash);
            }
            idsToRemove.Clear();
            GarbageCollector.CollectIncremental(GarbageCollector.incrementalTimeSliceNanoseconds);
            Resources.UnloadUnusedAssets();
        }
        private static void LimitRemovedCount(byte maxRemoveCount)
        {
            if (maxRemoveCount == 0 || idsToRemove.Count <= maxRemoveCount)
                return;
            idsToRemoveStrings.Clear();
            var removeCount = idsToRemove.Count - maxRemoveCount;
            var sortedDict = from entry in idsToRemove orderby entry.Value ascending select entry;
            foreach (var item in sortedDict.Take(removeCount))
                idsToRemoveStrings.Add(item.Key);
            foreach (var item in idsToRemoveStrings)
                idsToRemove.Remove(item);
            idsToRemoveStrings.Clear();
        }

        [HarmonyPatch(typeof(CVRDownloadManager), nameof(CVRDownloadManager.QueueTask))]
        class DownloadTaskPatch
        {
            static bool Prefix(string assetId, DownloadTask.ObjectType type, string assetUrl, string fileId, long fileSize, string fileKey, string toAttach, string fileHash = null,
                UgcTagsData tagsData = null, CVRLoadingAvatarController loadingAvatarController = null, bool joinOnComplete = false)
            {
                if (!enableMod.Value || (type != DownloadTask.ObjectType.Avatar && type != DownloadTask.ObjectType.Prop))
                    return true;
                if (enableLog.Value)
                    MelonLogger.Msg($"Downloading asset '{type}': assetId: '{assetId}', fileId: '{fileId}', fileHash: '{fileHash}', fileSize: {fileSize}, assetUrl empty? {string.IsNullOrEmpty(assetUrl)}, tags: { new Tags(tagsData) }.");
                if (string.IsNullOrEmpty(fileId) && fileIds.TryGetValue(StringToLongHash(fileKey), out string FileId))
                    fileId = FileId;
                var cacheKey = new CacheKey(assetId, fileId, StringToLongHash(fileKey));
                if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(assetId) && cachedAssets.TryGetValue(cacheKey, out var item))
                {
                    if (!item.IsMatch(type, assetId, fileId))
                    {
                        if (enableLog.Value)
                            MelonLogger.Msg($"Cache mismatch, downloading '{type}'.");
                        return true;
                    }
                    if (enableLog.Value)
                        MelonLogger.Msg($"Loading asset '{type}' from cache");
                    MelonCoroutines.Start(InstantiateItem(item, type, assetId, fileId, toAttach));
                    return false;
                }
                if (enableLog.Value)
                    MelonLogger.Msg($"Downloading '{type}'.");
                return true;
            }
        }
        private static ulong StringToLongHash(string text) 
        {
            ulong value = 0;
            unchecked
            {
                for (int i = 0; i < text.Length; i++)
                    value += text[i] * (ulong)(i + 1);
            }
            return value;
        }
        private static IEnumerator InstantiateItem(CacheItem item, DownloadTask.ObjectType type, string id, string fileId, string owner, bool wait = true)
        {
            if (wait)
            {
                if (enableGC.Value)
                    GarbageCollector.CollectIncremental(GarbageCollector.incrementalTimeSliceNanoseconds);
                yield return null;
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();

                while (instantiateSemaphore.CurrentCount == 0)
                {
                    if (loadTimeoutSecondsEntry.Value == 0 || sw.Elapsed.TotalSeconds < loadTimeoutSecondsEntry.Value)
                        yield return null;
                    else
                    {
                        MelonLogger.Error($"Timeout loading asset {type}: ID: '{id}', fileId: '{fileId}', owner: '{owner}'.");
                        yield break;
                    }
                } 
                while (!instantiateSemaphore.Wait(0)) 
                    yield return null;
            }
            if (enableLog.Value)
                MelonLogger.Msg($"Instantiating item {type}: ID: '{id}', fileId: '{fileId}', owner: '{owner}'.");
            GameObject parent;
            GameObject instance;
            List<GameObject> instanceList = new List<GameObject>(1);
            if (type == DownloadTask.ObjectType.Avatar)
            {
                bool local = owner == "_PLAYERLOCAL" || string.Equals(owner, MetaPort.Instance.ownerId);
                CVRPlayerEntity player = null;
                if (local)
                {
                    parent = PlayerSetup.Instance.PlayerAvatarParent;
                }
                else
                {
                    player = FindPlayer(owner);
                    parent = player?.AvatarHolder;
                }
                yield return InstantiateAvatar(item, instanceList, id, owner, parent, player, local);
                instance = instanceList.Count == 0 ? null : instanceList[0];
                if (!instance)
                {
                    MelonLogger.Error($"Instantiating item {type} failed: ID: '{id}', owner: '{owner}'.");
                    if (wait)
                        instantiateSemaphore.Release();
                    yield break;
                }
                if (player != null && player.LoadingAvatar != null)
                    GameObject.Destroy(player.LoadingAvatar);
                ActivateInstance(item, parent, instance);
                SetupInstantiatedAvatar(instance, player);
                if (local)
                {
                    CVR_MenuManager.Instance.UpdateMenuPosition();
                    ViewManager.Instance.UpdateMenuPosition(true);
                }
                yield return null;
            }
            else if (type == DownloadTask.ObjectType.Prop)
            {
                var propData = FindProp(owner);
                if (propData == null)
                {
                    MelonLogger.Error($"Instantiating item {type} failed: ID: '{id}', owner: '{owner}'. Prop data not found. count: { (CVRSyncHelper.Props?.Count.ToString() ?? "-null-")}'.");
                    yield break;
                }
                parent = new GameObject(owner);
                //MelonLoader.MelonLogger.Msg($"item: {item != null}, parent: {parent}, owner: {owner}, propData: {propData != null}, {propData?.InstanceId}");
                yield return InstantiateProp(item, instanceList, owner, parent, propData);
                instance = instanceList.Count == 0 ? null : instanceList[0];
                if (!instance)
                {
                    MelonLogger.Error($"Instantiating item {type} failed: ID: '{id}', owner: '{owner}', instances.Count: '{instanceList.Count}'.");
                    if (wait) 
                        instantiateSemaphore.Release();
                    yield break;
                }
                yield return null;
                ActivateInstance(item, parent, instance);
                SetPropData(propData, parent, instance);
            }
            if (wait)
            {
                yield return null;
                instantiateSemaphore.Release();
            }
        }
        private static void ActivateInstance(CacheItem item, GameObject parent, GameObject instance)
        {
            if (!parent || !instance)
                return;
            try
            {
                var instanceTransform = instance.transform;
                var parentTransform = parent.transform;
                if (instanceTransform.parent != parentTransform)
                {
                    instanceTransform.SetParent(parentTransform);
                    instanceTransform.SetPositionAndRotation(parentTransform.position, parentTransform.rotation);
                }
                instance.SetActive(item.WasEnabled);
                parent.SetActive(true);
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }
        private static IEnumerator InstantiateAvatar(CacheItem item, List<GameObject> instances, string assetId, string owner, GameObject parent, CVRPlayerEntity player, bool isLocal)
        {
            var oldGameObjectCount = parent.transform.childCount;
            var oldGameObjects = new GameObject[oldGameObjectCount];
            for (int i = 0; i < oldGameObjectCount; i++)
            {
                oldGameObjects[i] = parent.transform.GetChild(i).gameObject;
            }

            GameObject tempParent = new GameObject();
            tempParent.SetActive(false);
            tempParent.transform.SetParent(parent.transform);
            GameObject instance;
            if (isLocal)
            {
                if (enableLog.Value)
                    MelonLogger.Msg($"Local avatar: owner: '{owner}', MetaPort.Instance.ownerId: {MetaPort.Instance.ownerId}.");
                yield return item.GetSanitizedAvatar(tempParent, instances, item.Tags, assetId, isLocal: true);
                if (instances.Count == 0 || !instances[0])
                {
                    MelonLogger.Error($"Instantiating avatar failed: ID: '{assetId}', owner: local.");
                    yield break;
                }
                yield return null;
                yield return new WaitForEndOfFrame();
                MetaPort.Instance.currentAvatarGuid = assetId;
                PlayerSetup.Instance.ClearAvatar();
                instance = instances[0];
                ActivateNewGameObject(instance, parent, oldGameObjects, tempParent);
                yield break;
            }
            if (player == null || !player.AvatarHolder)
            {
                MelonLogger.Error($"Cannot instantiate avatar: player not found. id: '{owner}'.");
                yield break;
            }
            if (player.LoadingAvatar && player.LoadingAvatar.task != null)
                player.LoadingAvatar.task.Status = DownloadTask.ExecutionStatus.Complete;
            if (!parent)
            {
                MelonLogger.Error($"Cannot instantiate avatar: avatar holder not found. id: '{owner}'.");
                yield break;
            }
            bool friendsWith = FriendsWith(owner);
            bool avatarVisibility = MetaPort.Instance.SelfModerationManager.GetAvatarVisibility(owner, assetId, out bool forceHidden, out bool forceShow);
            string blockReason = string.Empty;
            avatarVisibility = avatarVisibility && ABI_RC.Core.Util.AssetFiltering.AssetFilter.GetAvatarVisibility(ref blockReason, owner, item.Tags.AvatarTags, friendsWith, forceShow, forceHidden, null);
            if (enableLog.Value && !string.IsNullOrEmpty(blockReason))
                MelonLogger.Msg($"Avatar hidden, reason: {blockReason}. ID: {assetId}, owner ID: {owner}");
            forceShow = forceShow && avatarVisibility;
            forceHidden = forceHidden && !avatarVisibility;
            yield return item.GetSanitizedAvatar(tempParent, instances, item.Tags, assetId, isLocal: false, friendsWith: friendsWith, isVisible: avatarVisibility, forceShow: forceShow, forceBlock: forceHidden);
            if (instances.Count == 0 || !instances[0])
            {
                MelonLogger.Error($"Instantiating avatar failed: ID: '{assetId}', owner: '{owner}'.");
                yield break;
            }
            yield return null;
            yield return new WaitForEndOfFrame();
            //delete current avatar, disable parent, move new to proper parent
            instance = instances[0];
            ActivateNewGameObject(instance, parent, oldGameObjects, tempParent);
            SetPlayerCapsuleSize(player, instance);

        }

        private static void ActivateNewGameObject(GameObject instance, GameObject parent, GameObject[] oldGameObjects, GameObject tempParent)
        {
            parent.SetActive(false);
            CacheItem.ClearTransformChildren(oldGameObjects);
            Transform parentTransform = parent.transform;
            instance.transform.SetParent(parentTransform);
            instance.transform.SetPositionAndRotation(parentTransform.position, parentTransform.rotation);
            GameObject.DestroyImmediate(tempParent);
        }

        private static IEnumerator InstantiateProp(CacheItem item, List<GameObject> instanceList, string target, GameObject parent, CVRSyncHelper.PropData propData)
        {
            if (propData == null || item == null)
            {
                if (enableLog.Value) 
                    MelonLogger.Error($"InstantiateProp failed, propData '{propData == null}', item: '{item == null}'.");
                yield break;
            }
            parent.transform.SetPositionAndRotation(new Vector3(propData.PositionX, propData.PositionY, propData.PositionZ),
                Quaternion.Euler(propData.RotationX, propData.RotationY, propData.RotationZ));
            yield return item.GetSanitizedProp(parent, instanceList, item.Tags, item.AssetId, target, propData.SpawnedBy);
        }
        private static void SetPropData(CVRSyncHelper.PropData propData, GameObject container, GameObject propInstance)
        {
            if (!propInstance)
                return;
            var component = propInstance.GetComponent<CVRSpawnable>();
            if (component == null || component.transform == null)
            {
                MelonLogger.Error($"SetPropData failed, component found? '{component == null}'.");
                return;
            }
            component.transform.localPosition = new Vector3(0f, component.spawnHeight, 0f);
            propData.Spawnable = component;
            propData.Wrapper = container;
            CVRSyncHelper.ApplyPropValuesSpawn(propData);
        }
        private static CVRSyncHelper.PropData FindProp(string owner)
        {
            var props = CVRSyncHelper.Props;
            for (int i = 0; i < props.Count; i++)
            {
                if (string.Equals(owner, props[i].InstanceId))
                    return props[i];
            }
            return null;
        }
        internal static bool FriendsWith(string owner)
        {
            if (string.IsNullOrEmpty(owner))
                return false;
            foreach (var friend in ViewManager.Instance.FriendList)
                if (string.Equals(owner, friend.UserId))
                    return true;
            return false;
        }
        private static void SetPlayerCapsuleSize(CVRPlayerEntity cvrplayerEntity, GameObject instance)
        {
            var playerCapsule = cvrplayerEntity.PlayerObject.GetComponent<CapsuleCollider>();
            var cvrAvatar = instance.GetComponent<CVRAvatar>();
            playerCapsule.height = cvrAvatar.viewPosition.y * 1.084f;
            playerCapsule.radius = playerCapsule.height / 6f;
            playerCapsule.center = new Vector3(0f, playerCapsule.height / 2f, 0f);
        }
        private static CVRPlayerEntity FindPlayer(string owner)
        {
            CVRPlayerEntity cvrplayerEntity = null;
            var players = CVRPlayerManager.Instance.NetworkPlayers;
            var length = players.Count;
            for (int i = 0; i < length; i++)
            {
                if (string.Equals(owner, players[i].Uuid))
                {
                    cvrplayerEntity = players[i];
                    break;
                }
            }
            return cvrplayerEntity;
        }

        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.OnDestroy))]
        class RemoveAvatarPatch
        {
            static void Postfix(CVRAvatar __instance)
            {
                if (!enableMod.Value)
                    return;
                var go = __instance.gameObject;
                if (enableLog.Value)
                    MelonLogger.Msg($"Deleting avatar {go.transform.root.name}, {cachedAssets.Count} cached assets.");
                CacheItem.RemoveInstance(go);
            }
        }
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.OnDestroy))]
        class RemovePropPatch
        {
            static void Postfix(CVRAvatar __instance)
            {
                if (!enableMod.Value)
                    return;
                var go = __instance.gameObject;
                if (enableLog.Value)
                    MelonLogger.Msg($"Deleting prop {go.transform.root.name}, {cachedAssets.Count} cached assets.");
                CacheItem.RemoveInstance(go);
            }
        }
 
        [HarmonyPatch(typeof(CVRAvatar))]
        class AvatarStartPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRAvatar.Start))]
            static bool InstantiateAvatarPrefix() => !enableMod.Value;
        }

        [HarmonyPatch(typeof(CVRObjectLoader))]
        class AddPatch
        {
            private static IEnumerator None() { yield break; }
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRObjectLoader.InstantiateAvatar))]
            static bool InstantiateAvatarPrefix(ref IEnumerator __result, DownloadTask.ObjectType t, AssetManagement.AvatarTags tags,
               string objectId, string instTarget, byte[] b, DownloadTask task)
            {
                if (!enableMod.Value)
                    return true;
                StartInstantiate(t, new Tags(tags), objectId, instTarget, b, task);
                __result = None();
                return false;
            }
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRObjectLoader.InstantiateProp))]
            static bool InstantiatePropPrefix(ref IEnumerator __result, DownloadTask.ObjectType t, AssetManagement.PropTags tags,
              string objectId, string instTarget, byte[] b, DownloadTask task)
            {
                if (!enableMod.Value)
                    return true;
                StartInstantiate(t, new Tags(tags), objectId, instTarget, b, task);
                __result = None();
                return false;
            }

            private static void StartInstantiate(DownloadTask.ObjectType t, Tags tags, string objectId, string instTarget, byte[] b, DownloadTask task)
            {
                if (enableLog.Value)
                    MelonLogger.Msg($"Instantiating asset '{t}': '{objectId}', '{task.FileId}', '{instTarget}', tags: {tags}.");
                if (task != null)
                    task.Status = DownloadTask.ExecutionStatus.Complete;
                fileIds[StringToLongHash(task.FileKey)] = task.FileId;
                MelonCoroutines.Start(LoadPatch(b, objectId, t, tags, instTarget, task.FileId, StringToLongHash(task.FileKey), task));
            }
        }
        private static void SetupInstantiatedAvatar(GameObject instance, CVRPlayerEntity player)
        {
            if (player != null)
            {
                var puppetMaster = player.PuppetMaster;
                if (!puppetMaster)
                {
                    MelonLogger.Error($"PuppetMaster is null for {player.PlayerNameplate}, {player.Username}, {player.Uuid}");
                    return;
                }
                puppetMaster.avatarObject = instance;
                puppetMaster.AvatarInstantiated();
            }
            else
                PlayerSetup.Instance.SetupAvatar(instance);
        }
        private static IEnumerator LoadPatch(byte[] b, string id, DownloadTask.ObjectType type, Tags tags, string owner, string FileId, ulong keyHash, DownloadTask job)
        {
            var assetPath = type == DownloadTask.ObjectType.Avatar ? _AVATAR_PATH : _PROP_PATH;
            if (b == null && type == DownloadTask.ObjectType.Avatar) 
            {
                var item = cachedAssets[BlockedKey];
                yield return InstantiateItem(item, type, item.AssetId, item.FileId, owner);
                job.Status = DownloadTask.ExecutionStatus.Failed;
                yield break;
            }
            
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var timeout = loadTimeoutSecondsEntry.Value;
            while (bundleLoadSemaphore.CurrentCount == 0)
            {
                if (sw.Elapsed.TotalSeconds < timeout)
                    yield return null;
                else
                    yield break;
            }
            while (!bundleLoadSemaphore.Wait(0))
                yield return null;
            bool shouldRelease = true;
            AssetBundleCreateRequest bundle = null;
            try
            {
                bundle = AssetBundle.LoadFromMemoryAsync(b);
                yield return bundle;
                if (bundle == null || !bundle.assetBundle)
                    yield break;
                if (type == DownloadTask.ObjectType.Avatar && owner != "_PLAYERLOCAL" && !CVRPlayerManager.Instance.TryGetConnected(owner))
                    yield break;
                if (!GetAssetName(assetPath, bundle.assetBundle.GetAllAssetNames(), out string assetName))
                    yield break;
                AssetBundleRequest asyncAsset;
                yield return asyncAsset = bundle.assetBundle.LoadAssetAsync(assetName, typeof(GameObject));
                var gameObject = (GameObject)asyncAsset.asset;
                if ((bundle?.assetBundle) != null && bundle != null)
                {
                    bundle.assetBundle.Unload(false);
                    bundleLoadSemaphore.Release();
                    shouldRelease = false;
                }

                var name = GetName(gameObject, FileId);
                var cacheKey = new CacheKey(id, FileId, keyHash);
                var item = cachedAssets[cacheKey] = new CacheItem(id, FileId, type, gameObject, tags, name);
                yield return InstantiateItem(item, type, id, FileId, owner, wait: false);
            }
            finally
            {
                if ((bundle?.assetBundle) != null && bundle != null)
                {
                    bundle.assetBundle.Unload(false);
                }
                if (shouldRelease)
                    bundleLoadSemaphore.Release();
            }
            job.Status = DownloadTask.ExecutionStatus.Complete;
            yield break;
        }

        private static bool GetAssetName(string expectedName, string[] allAssetNames, out string assetName)
        {
            if (allAssetNames == null || allAssetNames.Length == 0)
            {
                MelonLogger.Warning($"Bundle has no assets in it!");
                assetName = null;
                return false;
            }
            if (allAssetNames.Length > 1)
            {
                MelonLogger.Warning($"Bundle has multiple assets in it! Names: { string.Join(" ,", allAssetNames) }. Only the first one will be used!");
            }
            else if (!string.Equals(allAssetNames[0], expectedName))
            {
                MelonLogger.Warning($"Bundle's asset name is unexpected! { allAssetNames[0] }. It will be loaded anyway, but this would not work normally!");
            }
            assetName = allAssetNames[0];
            return true;
        }

        private static string GetName(GameObject gameObject, string defaultName)
        {
            var animator = gameObject.GetComponent<Animator>();
            if (!animator || !animator.isHuman || !animator.avatar)
                return defaultName;
            return animator.avatar.name.Replace("Avatar", "");
        } 
        /// <summary>
        /// Clears the memory cache, keep currently loaded assets.
        /// </summary>
        internal static void EmptyCache()
        {
            FlushCache = true;
            CleanupCache();
            FlushCache = false;
        }
        /// <summary>
        /// Release Semaphores if they are stuck and preventing loading of assets. Usable with Unity Explorer in case the mod breaks.
        /// </summary>
        internal static void ForceReleaseSemaphores() 
        {
            bundleLoadSemaphore.Release();
            instantiateSemaphore.Release();
        }
        /// <summary>
         /// Force clears the memory cache, including all loaded assets. Does not affect asset instances in the scene.
         /// </summary>
        internal static void ForceEmptyCache()
        {
            foreach (var item in cachedAssets)
            {
                cachedAssets[item.Key].Destroy();
            }
            fileIds.Clear();
            cachedAssets.Clear();
        }
    }
}