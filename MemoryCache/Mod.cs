using ABI.CCK.Components;
using ABI_RC.Core;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
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
        private static MelonPreferences_Entry<bool> enableLog;
        private static MelonPreferences_Entry<byte> maxLifeTimeMinutesEntry;
        private static MelonPreferences_Entry<byte> maxRemoveCountEntry;
        private static MelonPreferences_Entry<byte> loadTimeoutSecondsEntry;
        private static float maxLifeTimeMinutes = 15f;
        private static byte maxRemoveCount = 3;
        private static byte loadTimeoutSeconds = 30;
        private static readonly System.Threading.SemaphoreSlim bundleLoadSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private static readonly System.Threading.SemaphoreSlim instantiateSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, CacheItem> cachedAssets = new Dictionary<string, CacheItem>();
        private static readonly Dictionary<string, TimeSpan> idsToRemove = new Dictionary<string, TimeSpan>(10);
        private static readonly HashSet<string> idsToRemoveStrings = new HashSet<string>();
        const string BLOCKED_ID = "blocked";
        const string PROP_PATH = "assets/abi.cck/resources/cache/_cvrspawnable.prefab";
        const string AVATAR_PATH = "assets/abi.cck/resources/cache/_cvravatar.prefab";
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            enableMod = category.CreateEntry("enableMemoryCacheMod", true, "Enable MemoryCache mod");
            enableLog = category.CreateEntry("enableAvatarInstantiateLog", false, "Enable logging");
            maxLifeTimeMinutesEntry = category.CreateEntry("maxLifeTimeMinutesEntry", (byte)15, "Maximum lifetime of unused assets in cache (minutes)");
            maxRemoveCountEntry = category.CreateEntry("maxRemoveCountEntry", (byte)5, "Maximum number of assets to remove from cache per minute");
            loadTimeoutSecondsEntry = category.CreateEntry("loadTimeoutSecondsEntry", (byte)30, "Maximum time in seconds for assets to wait for loading before giving up");
            maxLifeTimeMinutesEntry.OnValueChanged += MaxLifeTimeMinutesEntry_OnValueChanged;
            maxRemoveCountEntry.OnValueChanged += MaxRemoveCountEntry_OnValueChanged;
            loadTimeoutSecondsEntry.OnValueChanged += LoadTimeoutSecondsEntry_OnValueChanged;
            enableMod.OnValueChanged += EnableMod_OnValueChanged;
        }
        private void EnableMod_OnValueChanged(bool arg1, bool arg2) => EmptyCache();
        private void LoadTimeoutSecondsEntry_OnValueChanged(byte arg1, byte arg2) => loadTimeoutSeconds = loadTimeoutSecondsEntry.Value;
        private void MaxRemoveCountEntry_OnValueChanged(byte oldValue, byte newValue) => maxLifeTimeMinutes = maxLifeTimeMinutesEntry.Value;
        private void MaxLifeTimeMinutesEntry_OnValueChanged(byte oldValue, byte newValue) => maxRemoveCount = maxRemoveCountEntry.Value;
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
                cachedAssets.Remove(BLOCKED_ID);
                cachedAssets[BLOCKED_ID] = new CacheItem(BLOCKED_ID, DownloadJob.ObjectType.Avatar, MetaPort.Instance?.blockedAvatarPrefab, true);
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
            var maxAge = TimeSpan.FromMinutes(maxLifeTimeMinutes);
            foreach (var id in keys)
            {
                var item = cachedAssets[id];
                var age = now - item.LastRefRemoved;
                if (item.IsEmpty && age > maxAge)
                    idsToRemove.Add(id, age);
            }
            if (idsToRemove.Count == 0)
                return;
            LimitRemovedCount(maxRemoveCount);
            string removedNumber = maxRemoveCount == 0 ? "no limit on the number of assets" : $"maximum number of assets to remove was {maxRemoveCount}";
            if (enableLog.Value)
                MelonLogger.Msg($"Clearing cache, removing {idsToRemove.Count} cached items. Maximum lifetime was {maxLifeTimeMinutes} minutes, {removedNumber}.");
            foreach (var item in idsToRemove)
            {
                cachedAssets[item.Key].Destroy();
                cachedAssets.Remove(item.Key);
            }
            idsToRemove.Clear();
            UnityEngine.Scripting.GarbageCollector.CollectIncremental(UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds);
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
                ABI_RC.Core.InteractionSystem.CVRLoadingAvatarController loadingAvatar)
            {
                if (!enableMod.Value)
                    return true;

                if (cachedAssets.TryGetValue(id, out var item))
                {
                    if (!item.IsMatch(type, id))
                        return true;
                    if (tags == null)
                        tags = new UgcTagsData();
                    MelonCoroutines.Start(InstantiateItem(item, type, id, owner, tags));
                    return false;
                }
                return true;
            }
        }

        private static IEnumerator InstantiateItem(CacheItem item, DownloadJob.ObjectType type, string id, string owner, UgcTagsData tags, bool wait = true)
        {
            if (wait)
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                while (!instantiateSemaphore.Wait(0))
                {
                    if (loadTimeoutSeconds == 0 || sw.Elapsed.TotalSeconds < loadTimeoutSeconds)
                        yield return null;
                    else
                    {
                        MelonLogger.Error($"Timeout loading asset {type}: ID: '{id}', owner: '{owner}'.");
                        yield break;
                    }
                }
            }
            MelonLogger.Msg($"Instantiating item {type}: ID: '{id}', owner: '{owner}'.");
            GameObject parent = null;
            GameObject instance;
            if (type == DownloadJob.ObjectType.Avatar)
            {
                instance = InstantiateAvatar(item, id, owner, tags, ref parent, out CVRPlayerEntity player);
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
                SetupInstantiatedAvatar(instance, player);
            }
            else if (type == DownloadJob.ObjectType.Prop)
            {
                instance = InstantiateProp(item, owner, tags, ref parent, out var propData);
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

        private static GameObject InstantiateAvatar(CacheItem item, string id, string owner, UgcTagsData tags, ref GameObject parent, out CVRPlayerEntity player)
        {
            GameObject instance;
            if (owner == "_PLAYERLOCAL" || string.Equals(owner, MetaPort.Instance.ownerId))
            {
                if (enableLog.Value)
                    MelonLogger.Msg($"Local avatar: owner: '{owner}', MetaPort.Instance.ownerId: {MetaPort.Instance.ownerId}.");
                MetaPort.Instance.currentAvatarGuid = id;
                parent = PlayerSetup.Instance.PlayerAvatarParent;
                PlayerSetup.Instance.ClearAvatar();
                instance = item.GetSanitizedAvatar(parent, tags, true);
                player = null;
                return instance;
            }
            player = FindPlayer(owner);
            if (player == null || !player.AvatarHolder)
            {
                MelonLogger.Error($"Cannot instantiate avatar: player not found. id: '{owner}'.");
                return null;
            }
            parent = player.AvatarHolder;
            if (!parent)
            {
                MelonLogger.Error($"Cannot instantiate avatar: avatar holder not found. id: '{owner}'.");
                return null;
            }
            if (player.LoadingAvatar != null)
                GameObject.Destroy(player.LoadingAvatar);
            bool friendsWith = FriendsWith(owner);
            bool avatarVisibility = MetaPort.Instance.SelfModerationManager.GetAvatarVisibility(owner, id, out bool forceBlock, out bool forceShow);
            forceShow = forceShow && avatarVisibility;
            forceBlock = forceBlock && !avatarVisibility;
            instance = item.GetSanitizedAvatar(parent, tags, false, friendsWith, forceShow, forceBlock);
            SetPlayerCapsuleSize(player, instance);
            return instance;
        }
        private static GameObject InstantiateProp(CacheItem item, string target, UgcTagsData tags, ref GameObject parent, out CVRSyncHelper.PropData propData)
        {
            propData = FindProp(target);
            if (propData == null || item == null)
                return null;
            parent = new GameObject(target);
            parent.transform.SetPositionAndRotation(new Vector3(propData.PositionX, propData.PositionY, propData.PositionZ),
                Quaternion.Euler(propData.RotationX, propData.RotationY, propData.RotationZ));
            bool? propVisibility = MetaPort.Instance.SelfModerationManager.GetPropVisibility(propData.SpawnedBy, target);
            bool isOwn = propData.SpawnedBy == MetaPort.Instance.ownerId || FriendsWith(propData.SpawnedBy);
            var propInstance = item.GetSanitizedProp(parent, tags, isOwn, propVisibility);
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
            var friendsList = ViewManager.Instance.FriendList;
            for (int i = 0; i < friendsList.Count; i++)
            {
                if (string.Equals(owner, friendsList[i].UserId))
                    return true;
            }
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

       
        private static void RemoveInstance(GameObject go)
        {
            foreach (var item in cachedAssets)
            {
                if (item.Value.HasInstance(go))
                {
                    item.Value.RemoveInstance(go);
                    break;
                }
            }
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
                if (!enableLog.Value)
                    MelonLogger.Msg($"Deleting {go.name}, {cachedAssets.Count} cached assets.");
                RemoveInstance(go);
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
                if (!enableLog.Value)
                    MelonLogger.Msg($"Deleting prop {go.transform.root.name}, {cachedAssets.Count} cached assets.");
                RemoveInstance(go);
            }
        }

        [HarmonyPatch(typeof(CVRObjectLoader))]
        class AddAvatarPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRObjectLoader.InstantiateAvatar))]
            static bool InstantiateAvatarPrefix(IEnumerator __result, DownloadJob.ObjectType t, AssetManagement.AvatarTags tags,
               string objectId, string instTarget, byte[] b)
            {
                if (!enableMod.Value)
                    return true;
                if (enableLog.Value)
                    MelonLogger.Msg($"Instantiating avatar: '{objectId}', '{instTarget}'.");
                MelonCoroutines.Start(LoadPatch(b, objectId, t, CacheItem.TagsConverter(tags), instTarget));
                return false;
            }
        }
        private static void SetupInstantiatedAvatar(GameObject instance, CVRPlayerEntity player)
        {
            if (player != null)
            {
                var puppetMaster = player.PuppetMaster;
                if (!puppetMaster)
                {
                    MelonLogger.Msg($"PuppetMaster is null for {player.PlayerNameplate}, {player.Username}, {player.Uuid}");
                    return;
                }
                puppetMaster.avatarObject = instance;
                puppetMaster.AvatarInstantiated();
            }
            else
                PlayerSetup.Instance.SetupAvatar(instance);
        }
        private static IEnumerator LoadPatch(byte[] b, string id, DownloadJob.ObjectType type, UgcTagsData tags, string owner)
        {
            var assetPath = type == DownloadJob.ObjectType.Avatar ? AVATAR_PATH : PROP_PATH;
            if (tags == null)
                tags = new UgcTagsData();
            GameObject parent = null;
            GameObject instance;
            if (b == null) 
            {
                instance = InstantiateAvatar(cachedAssets[BLOCKED_ID], BLOCKED_ID, owner, tags, ref parent, out CVRPlayerEntity player);
                yield return null;
                if (parent)
                    parent.SetActive(true);
                SetupInstantiatedAvatar(instance, player);
                yield break;
            }
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                while (!bundleLoadSemaphore.Wait(0))
                {
                    if (sw.Elapsed.TotalSeconds < loadTimeoutSeconds)
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
                var item = cachedAssets[id] = new CacheItem(id, type, gameObject);
                yield return InstantiateItem(item, type, id, owner, tags, wait: false);
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
        /// <summary>
        /// Clears the memory cache, keep currently loaded assets.
        /// </summary>
        internal static void EmptyCache()
        {
            var oldLifeTimeMinutes = maxLifeTimeMinutes;
            var oldRemoveCount = maxRemoveCount;
            maxRemoveCount = 0;
            maxLifeTimeMinutes = 0f;
            CleanupCache();
            maxRemoveCount = oldRemoveCount;
            maxLifeTimeMinutes = oldLifeTimeMinutes;
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
            cachedAssets.Clear();
        }
    }
}
