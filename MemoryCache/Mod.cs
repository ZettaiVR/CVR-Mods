using ABI.CCK.Components;
using ABI_RC.Core;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
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
        private static readonly Guid BLOCKED_GUID = new Guid("E765F452-F372-4ECE-B362-1F48E64D2F7E");
        private static readonly CacheKey BlockedKey = new CacheKey(BLOCKED_GUID, 0, 0);
        const string BLOCKED_NAME = "Blocked";
        const string BLOCKED_VERSION = "000000000000";
        const string PROP_PATH = "assets/abi.cck/resources/cache/_cvrspawnable.prefab";
        const string AVATAR_PATH = "assets/abi.cck/resources/cache/_cvravatar.prefab";
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
                cachedAssets[BlockedKey] = new CacheItem(BLOCKED_NAME, BLOCKED_VERSION, DownloadJob.ObjectType.Avatar, MetaPort.Instance?.blockedAvatarPrefab, new Tags(), BLOCKED_NAME, true);
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

        [HarmonyPatch(typeof(CVRDownloadManager), nameof(CVRDownloadManager.AddDownloadJob))]
        class DownloadJobPatch
        {
            static bool Prefix(DownloadJob.ObjectType type, string id, string owner, string key, long size,
                bool joinOnComplete, string hash, string fileID, string location, ABI_RC.Core.Networking.API.Responses.UgcTagsData tags,
                CVRLoadingAvatarController loadingAvatar)
            {
                if (!enableMod.Value || (type != DownloadJob.ObjectType.Avatar && type != DownloadJob.ObjectType.Prop))
                    return true;
                if (enableLog.Value)
                    MelonLogger.Msg($"Downloading asset '{type}': '{id}', '{fileID}', '{hash}', tags: { new Tags(tags) }.");
                if (string.IsNullOrEmpty(fileID) && fileIds.TryGetValue(StringToLongHash(key), out string objectFileId))
                    fileID = objectFileId;
                var cacheKey = new CacheKey(id, fileID, StringToLongHash(key));
                if (!string.IsNullOrEmpty(fileID) && !string.IsNullOrEmpty(id) && cachedAssets.TryGetValue(cacheKey, out var item))
                {
                    if (!item.IsMatch(type, id, fileID))
                    {
                        if (enableLog.Value)
                            MelonLogger.Msg($"Cache mismatch, downloading '{type}'.");
                        return true;
                    }
                    if (enableLog.Value)
                        MelonLogger.Msg($"Loading asset '{type}' from cache");
                    MelonCoroutines.Start(InstantiateItem(item, type, id, fileID, owner));
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
        private static IEnumerator InstantiateItem(CacheItem item, DownloadJob.ObjectType type, string id, string fileId, string owner, bool wait = true)
        {
            if (wait)
            {
                if (enableGC.Value)
                    GarbageCollector.CollectIncremental(GarbageCollector.incrementalTimeSliceNanoseconds);
                yield return null;
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                while (!instantiateSemaphore.Wait(0))
                {
                    if (loadTimeoutSecondsEntry.Value == 0 || sw.Elapsed.TotalSeconds < loadTimeoutSecondsEntry.Value)
                        yield return null;
                    else
                    {
                        MelonLogger.Error($"Timeout loading asset {type}: ID: '{id}', fileId: {fileId}, owner: '{owner}'.");
                        yield break;
                    }
                }
            }
            if (enableLog.Value)
                MelonLogger.Msg($"Instantiating item {type}: ID: '{id}', fileId: {fileId}, owner: '{owner}'.");
            GameObject parent = null;
            GameObject instance;
            if (type == DownloadJob.ObjectType.Avatar)
            {
                instance = InstantiateAvatar(item, id, owner, ref parent, out CVRPlayerEntity player);
                if (!instance)
                {
                    MelonLogger.Error($"Instantiating item {type} failed: ID: '{id}', owner: '{owner}'.");
                    if (wait) 
                        instantiateSemaphore.Release();
                    yield break;
                }
                yield return null;
                if (player != null && player.LoadingAvatar != null)
                    GameObject.Destroy(player.LoadingAvatar);
                if (parent)
                    parent.SetActive(true);
                SetupInstantiatedAvatar(instance, player);
                CVR_MenuManager.Instance.UpdateMenuPosition();
                ViewManager.Instance.UpdateMenuPosition(true);
            }
            else if (type == DownloadJob.ObjectType.Prop)
            {
                instance = InstantiateProp(item, owner, ref parent, out var propData);
                if (!instance)
                {
                    MelonLogger.Error($"Instantiating item {type} failed: ID: '{id}', owner: '{owner}'.");
                    if (wait) 
                        instantiateSemaphore.Release();
                    yield break;
                }
                yield return null;
                if (parent)
                    parent.SetActive(true);
                SetPropData(propData, parent, instance);
            }
            if (wait)
            {
                yield return null;
                instantiateSemaphore.Release();
            }
            yield break;
        }
        private static GameObject InstantiateAvatar(CacheItem item, string id, string owner, ref GameObject parent, out CVRPlayerEntity player)
        {
            GameObject instance;
            if (owner == "_PLAYERLOCAL" || string.Equals(owner, MetaPort.Instance.ownerId))
            {
                if (enableLog.Value)
                    MelonLogger.Msg($"Local avatar: owner: '{owner}', MetaPort.Instance.ownerId: {MetaPort.Instance.ownerId}.");
                MetaPort.Instance.currentAvatarGuid = id;
                parent = PlayerSetup.Instance.PlayerAvatarParent;
                PlayerSetup.Instance.ClearAvatar();
                instance = item.GetSanitizedAvatar(parent, item.Tags, true);
                player = null;
                return instance;
            }
            player = FindPlayer(owner);
            if (player == null || !player.AvatarHolder)
            {
                MelonLogger.Error($"Cannot instantiate avatar: player not found. id: '{owner}'.");
                return null;
            }
            if (player.LoadingAvatar && player.LoadingAvatar.job != null)
                player.LoadingAvatar.job.Status = DownloadJob.ExecutionStatus.Instantiating;
            parent = player.AvatarHolder;
            if (!parent)
            {
                MelonLogger.Error($"Cannot instantiate avatar: avatar holder not found. id: '{owner}'.");
                return null;
            }
            bool friendsWith = FriendsWith(owner);
            bool avatarVisibility = MetaPort.Instance.SelfModerationManager.GetAvatarVisibility(owner, id, out bool forceBlock, out bool forceShow);
            forceShow = forceShow && avatarVisibility;
            forceBlock = forceBlock && !avatarVisibility;
            instance = item.GetSanitizedAvatar(parent, item.Tags, false, friendsWith, forceShow, forceBlock);
            SetPlayerCapsuleSize(player, instance);
            return instance;
        }
        private static GameObject InstantiateProp(CacheItem item, string target, ref GameObject parent, out CVRSyncHelper.PropData propData)
        {
            propData = FindProp(target);
            if (propData == null || item == null)
                return null;
            parent = new GameObject(target);
            parent.transform.SetPositionAndRotation(new Vector3(propData.PositionX, propData.PositionY, propData.PositionZ),
                Quaternion.Euler(propData.RotationX, propData.RotationY, propData.RotationZ));
            bool? propVisibility = MetaPort.Instance.SelfModerationManager.GetPropVisibility(propData.SpawnedBy, target);
            bool isOwn = propData.SpawnedBy == MetaPort.Instance.ownerId || FriendsWith(propData.SpawnedBy);
            var propInstance = item.GetSanitizedProp(parent, item.Tags, isOwn, propVisibility);
            return propInstance;
        }
        private static void SetPropData(CVRSyncHelper.PropData propData, GameObject container, GameObject propInstance)
        {
            var component = propInstance.GetComponent<CVRSpawnable>();
            if (component == null)
                return;
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
        private static bool FriendsWith(string owner)
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

        [HarmonyPatch(typeof(RootLogic), nameof(RootLogic.SpawnOnWorldInstance))]
        class SpawnOnWorldInstancePatch
        {
            static void Postfix()
            {
                if (!enableMod.Value)
                    return;
                EmptyCache();
            }
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
                    MelonLogger.Msg($"Deleting {go.name}, {cachedAssets.Count} cached assets.");
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
            static bool InstantiateAvatarPrefix(ref IEnumerator __result, DownloadJob.ObjectType t, AssetManagement.AvatarTags tags,
               string objectId, string instTarget, byte[] b, DownloadJob job)
            {
                if (!enableMod.Value)
                    return true;
                StartInstantiate(t, new Tags(tags), objectId, instTarget, b, job);
                __result = None();
                return false;
            }
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRObjectLoader.InstantiateProp))]
            static bool InstantiatePropPrefix(ref IEnumerator __result, DownloadJob.ObjectType t, AssetManagement.PropTags tags,
              string objectId, string instTarget, byte[] b, DownloadJob job)
            {
                if (!enableMod.Value)
                    return true;
                StartInstantiate(t, new Tags(tags), objectId, instTarget, b, job);
                __result = None();
                return false;
            }

            private static void StartInstantiate(DownloadJob.ObjectType t, Tags tags, string objectId, string instTarget, byte[] b, DownloadJob job)
            {
                if (enableLog.Value)
                    MelonLogger.Msg($"Instantiating asset '{t}': '{objectId}', '{job.ObjectFileId}', '{instTarget}', tags: {tags}.");
                if (job != null)
                    job.Status = DownloadJob.ExecutionStatus.Instantiating;
                fileIds[StringToLongHash(job.FileKey)] = job.ObjectFileId;
                MelonCoroutines.Start(LoadPatch(b, objectId, t, tags, instTarget, job.ObjectFileId, StringToLongHash(job.FileKey)));
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
        private static IEnumerator LoadPatch(byte[] b, string id, DownloadJob.ObjectType type, Tags tags, string owner, string objectFileId, ulong keyHash)
        {
            var assetPath = type == DownloadJob.ObjectType.Avatar ? AVATAR_PATH : PROP_PATH;
            GameObject parent = null;
            GameObject instance;
            if (b == null && type == DownloadJob.ObjectType.Avatar) 
            {
                instance = InstantiateAvatar(cachedAssets[BlockedKey], BLOCKED_NAME, owner, ref parent, out CVRPlayerEntity player);
                yield return null;
                if (parent)
                    parent.SetActive(true);
                SetupInstantiatedAvatar(instance, player);
                yield break;
            }
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                var timeout = loadTimeoutSecondsEntry.Value;
                while (!bundleLoadSemaphore.Wait(0))
                {
                    if (sw.Elapsed.TotalSeconds < timeout)
                        yield return null;
                    else
                        yield break;
                }
            }
            bool shouldRelease = true;
            AssetBundleCreateRequest bundle = null;
            try
            {
                bundle = AssetBundle.LoadFromMemoryAsync(b);
                yield return bundle;
                if (bundle == null || !bundle.assetBundle)
                    yield break;
                if (type == DownloadJob.ObjectType.Avatar && !CVRPlayerManager.Instance.TryGetConnected(owner) && owner != "_PLAYERLOCAL")
                    yield break;
                AssetBundleRequest asyncAsset;
                yield return asyncAsset = bundle.assetBundle.LoadAssetAsync(assetPath, typeof(GameObject));
                var gameObject = (GameObject)asyncAsset.asset;
                if ((bundle?.assetBundle) != null && bundle != null)
                {
                    bundle.assetBundle.Unload(false);
                    bundleLoadSemaphore.Release();
                    shouldRelease = false;
                }
                
                var name = GetName(gameObject, objectFileId);
                var cacheKey = new CacheKey(id, objectFileId, keyHash);
                var item = cachedAssets[cacheKey] = new CacheItem(id, objectFileId, type, gameObject, tags, name);
                yield return InstantiateItem(item, type,id, objectFileId, owner, wait: false);
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
            yield break;
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