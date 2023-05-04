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
using System.Threading;

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
        internal static MelonPreferences_Entry<bool> enableDownloader;
        internal static MelonPreferences_Entry<bool> checkHash;
        internal static MelonPreferences_Entry<byte> downloadThreads;
        internal static MelonPreferences_Entry<byte> verifyThreads;
        internal static MelonPreferences_Entry<bool> enableGC;
        internal static MelonPreferences_Entry<bool> enablePerfStat;
        private static MelonPreferences_Entry<byte> maxLifeTimeMinutesEntry;
        private static MelonPreferences_Entry<byte> maxRemoveCountEntry;
        private static MelonPreferences_Entry<ushort> loadTimeoutSecondsEntry;
        private static bool FlushCache = false;
        internal static bool verifierEnabled = false;
        internal static bool publicOnly = false;
        internal static bool isPublic = false;

        private static readonly SemaphoreSlim bundleLoadSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim instantiateSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim downloadSemaphore = new SemaphoreSlim(0, 32);
        private static readonly Dictionary<string, CacheKey> playerIdToAsset = new Dictionary<string, CacheKey>();
        private static readonly Dictionary<CacheKey, CacheItem> cachedAssets = new Dictionary<CacheKey, CacheItem>();
        private static readonly Dictionary<CacheKey, DownloadData> downloadTasks = new Dictionary<CacheKey, DownloadData>();
        private static readonly Dictionary<CacheKey, TimeSpan> idsToRemove = new Dictionary<CacheKey, TimeSpan>(10);
        private readonly static Dictionary<ulong, string> fileIds = new Dictionary<ulong, string>();
        private static readonly HashSet<CacheKey> idsToRemoveStrings = new HashSet<CacheKey>();
        private static readonly Dictionary<string, LoadTask> loadingTasks = new Dictionary<string, LoadTask>();
        private static readonly Guid BLOCKED_GUID = new Guid("B10CED00-F372-4ECE-B362-1F48E64D2F7E");
        private static readonly CacheKey BlockedKey = new CacheKey(BLOCKED_GUID, 0, 0);
        private static int threadCount = 1;
        const string _BLOCKED_NAME = "Blocked";
        const string _BLOCKED_VERSION = "000000000000";
        const string _PROP_PATH = "assets/abi.cck/resources/cache/_cvrspawnable.prefab";
        const string _AVATAR_PATH = "assets/abi.cck/resources/cache/_cvravatar.prefab";
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            enableMod = category.CreateEntry("enableMemoryCacheMod", true, "Enable MemoryCache mod");
            enableLog = category.CreateEntry("enableMemoryCacheLog", false, "Enable MemoryCache logging");
            downloadThreads = category.CreateEntry("downloadThreads", (byte)5, "Download threads");
            verifyThreads = category.CreateEntry("verifyThreads", (byte)5, "Bundle Verifier threads");
            downloadSemaphore.Release(downloadThreads.Value);
            FileCache.Init(downloadThreads.Value);
            enableGC = category.CreateEntry("enableGC", false, "Enable GC");
            enableOwnSanitizer = category.CreateEntry("enableOwnSanitizer", false, "Enable MemoryCache sanitizer");
            enableDownloader = category.CreateEntry("enableDownloader", false, "Enable MemoryCache downloader");
            enablePerfStat = category.CreateEntry("enablePerfStat", false, "Enable logging performance stats");
            checkHash = category.CreateEntry("checkHash", true, "Check file hash");
            enableHologram = category.CreateEntry("enableHologram", true, "Enable Hologram (?)");
            maxLifeTimeMinutesEntry = category.CreateEntry("maxLifeTimeMinutesEntry", (byte)15, "Maximum lifetime of unused assets in cache (minutes)");
            maxRemoveCountEntry = category.CreateEntry("maxRemoveCountEntry", (byte)5, "Maximum number of assets to remove from cache per minute");
            loadTimeoutSecondsEntry = category.CreateEntry("loadTimeoutSecondsEntry", (ushort)60, "Loat timeout (0 = disable)");
            enableMod.OnValueChanged += EnableMod_OnValueChanged;
            threadCount = Environment.ProcessorCount > 10 ? 2 : 1;
        }
        public override void OnLateUpdate()
        {
            UpdateLoadingAvatars();
            FileCache.StartTasks(downloadThreads.Value, threadCount, verifyThreads.Value, threadCount);
            verifierEnabled = MetaPort.Instance?.settings.GetSettingsBool("ExperimentalBundleVerifierEnabled", false) ?? false;
            publicOnly = MetaPort.Instance?.settings.GetSettingsBool("ExperimentalBundleVerifierPublicsOnly", false) ?? false;
            isPublic = string.Equals(MetaPort.Instance?.CurrentInstancePrivacy, "public", StringComparison.OrdinalIgnoreCase);
        }
        public override void OnApplicationQuit()
        {
            FileCache.AbortAllThreads();
        }
        
        private void EnableMod_OnValueChanged(bool arg1, bool arg2) => EmptyCache();
        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => UpdateBlockedAvatar();
        public override void OnApplicationLateStart()
        {
            UpdateBlockedAvatar();
            SchedulerSystem.AddJob(new SchedulerSystem.Job(CleanupCache), 5f, 60f, -1);
        }

        [HarmonyPatch(typeof(CVRLoadingAvatarController), nameof(CVRLoadingAvatarController.Update))]
        class LoadingAvatarPatch
        {
            static bool Prefix() => !enableDownloader.Value;
        }
        [HarmonyPatch(typeof(CVRPlayerManager))]
        class ReloadAvatarPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRPlayerManager.ReloadAvatar))]
            public static bool ReloadAvatarPrefix(string avatarId)
            {
                if (string.IsNullOrEmpty(avatarId) ||
                    (PlayerSetup.Instance._avatarDescriptor && string.Equals(avatarId, MetaPort.Instance.currentAvatarGuid, StringComparison.OrdinalIgnoreCase)))
                    return true;
                if (!enableMod.Value && cachedAssets.Count == 0)
                    return true;
                var networkPlayers = CVRPlayerManager.Instance.NetworkPlayers;
                try
                {
                    ReloadPlayersWithAvatar(avatarId, null, networkPlayers);
                }
                catch (Exception e) 
                {
                    MelonLogger.Error($"Error reloading players with avatar ID '{avatarId}'.", e);
                }
                return false;
            }
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRPlayerManager.ReloadAllAvatars))]
            public static bool ReloadAllAvatarsPrefix()
            {
                if (!enableMod.Value && cachedAssets.Count == 0)
                    return true;
                var networkPlayers = CVRPlayerManager.Instance.NetworkPlayers;
                try
                {
                    ReloadPlayersWithAvatar(null, null, networkPlayers);
                }
                catch (Exception e)
                {
                    MelonLogger.Error("Error reloading all players.", e);
                }
                return false;
            }
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRPlayerManager.ReloadPlayersAvatar))]
            public static bool ReloadPlayersAvatarPrefix(string userId)
            {
                if (!enableMod.Value && cachedAssets.Count == 0)
                    return true;

                var networkPlayers = CVRPlayerManager.Instance.NetworkPlayers;
                try
                {
                    ReloadPlayersWithAvatar(null, userId, networkPlayers);
                }
                catch (Exception e)
                {
                    MelonLogger.Error("Error reloading local player.", e);
                }
                return false;
            }
            private static void ReloadPlayersWithAvatar(string avatarId, string userId, List<CVRPlayerEntity> networkPlayers)
            {
                bool hasAvatarId = !string.IsNullOrEmpty(avatarId);
                bool hasUserId = !string.IsNullOrEmpty(userId);
                foreach (var player in networkPlayers)
                {
                    if (string.IsNullOrEmpty(player?.Uuid))
                        continue;
                    if (hasAvatarId && !string.Equals(player?.AvatarId, avatarId))
                        continue; 
                    if (hasUserId && !string.Equals(player?.Uuid, userId))
                        continue;
                    var pm = player.PuppetMaster;
                    if (pm._avatar == null)
                        continue;

                    MTJobSystem.MTJobManager.AbortCollection(player.Uuid);
                    CVRDownloadManager.Instance.RemoveQueuedUser(player.Uuid);
                    pm.AvatarDestroyed();
                    CacheItem.ClearTransformChildren(player.AvatarHolder);
                    string url = null;
                    string fileId = null;
                    string fileHash = null;
                    UgcTagsData tags = null;
                    if (!hasAvatarId)
                        avatarId = player.AvatarId;
                    foreach (var item in downloadTasks)
                    {
                        if (item.Value.assetId == avatarId)
                        {
                            url = item.Value.assetUrl;
                            fileId = item.Value.fileId;
                            fileHash = item.Value.fileHash;
                            tags = item.Value.tags.UgcTagsData;
                            item.Value.cancellationToken.Cancel();
                            downloadTasks.Remove(item.Key);
                            break;
                        }
                    }
                    if (url == null) 
                    {
                        var dlTasks = CVRDownloadManager.Instance._cachedTasks;
                        if (dlTasks.TryGetValue(avatarId, out var downloadTask)) 
                        {
                            url = downloadTask.FileLocation;
                            fileId = downloadTask.FileId;
                            fileHash = downloadTask.FileHash;
                            tags = downloadTask.TagsData;
                        }
                    }
                    if (enableMod.Value)
                    {
                        DownloadTaskPatch.Prefix(player.AvatarId,
                            DownloadTask.ObjectType.Avatar,
                            url,
                            fileId,
                            player.AvatarFileSize,
                            player.AvatarFileKey,
                            player.Uuid,
                            fileHash,
                            tags,
                            player.LoadingAvatar,
                            false);
                    }
                    else 
                    {
                        CVRDownloadManager.Instance.QueueTask(
                            player.AvatarId,
                            DownloadTask.ObjectType.Avatar,
                            url,
                            fileId,
                            player.AvatarFileSize,
                            player.AvatarFileKey,
                            player.Uuid,
                            fileHash,
                            tags,
                            player.LoadingAvatar,
                            false);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CVRDownloadManager), nameof(CVRDownloadManager.QueueTask))]
        class DownloadTaskPatch
        {
            private static int DLCounter = 0;
#pragma warning disable IDE0060 // Remove unused parameter
            public static bool Prefix(string assetId, DownloadTask.ObjectType type, string assetUrl, string fileId, long fileSize, string fileKey, string toAttach, string fileHash,
                UgcTagsData tagsData, CVRLoadingAvatarController loadingAvatarController, bool joinOnComplete = false)
#pragma warning restore IDE0060 // Remove unused parameter
            {
                if (!enableMod.Value || (type != DownloadTask.ObjectType.Avatar && type != DownloadTask.ObjectType.Prop))
                    return true;
                var thisDl = DLCounter.ToString("D4");
                DLCounter++;
                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Downloading asset '{type}': assetId: '{assetId}', fileId: '{fileId}', fileHash: '{fileHash}', fileSize: {fileSize}, toAttach: '{toAttach}' assetUrl empty? {string.IsNullOrEmpty(assetUrl)}, tags: {( tagsData != null ? new Tags(tagsData).ToString() : "-null-") }.");

                if (string.IsNullOrEmpty(fileId) && fileIds.TryGetValue(StringToLongHash(fileKey), out string FileId))
                    fileId = FileId;
                var cacheKey = new CacheKey(assetId, fileId, StringToLongHash(fileKey));
                playerIdToAsset[toAttach] = cacheKey;
                if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(assetId) && cachedAssets.TryGetValue(cacheKey, out var item))
                {
                    if (!item.IsMatch(type.ToAssetType(), assetId, fileId))
                    {
                        if (enableLog.Value)
                            MelonLogger.Msg($"[DL-{thisDl}] Cache mismatch, downloading '{type}'. assetId '{assetId}' fileId '{fileId}' item.AssetId '{item.AssetId}' item.FileId '{item.FileId}'. ");
                        if (enableDownloader.Value)
                        {
                            return StartDownloadAsset(cacheKey, assetId, type, assetUrl, fileId, fileSize, fileKey, toAttach, fileHash, tagsData, joinOnComplete, thisDl);
                        }
                        return true;
                    }
                    if (enableLog.Value)
                        MelonLogger.Msg($"[DL-{thisDl}] Loading asset '{type}' from cache");
                    MelonCoroutines.Start(InstantiateItem(item, type, assetId, fileId, toAttach, thisDl, cacheKey));
                    return false;
                }
                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Asset not cached, start downloading '{type}' assetId '{assetId}' fileId '{fileId}'. ");
                if (enableDownloader.Value)
                {
                    return StartDownloadAsset(cacheKey, assetId, type, assetUrl, fileId, fileSize, fileKey, toAttach, fileHash, tagsData, joinOnComplete, thisDl);
                }
                return true;
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
        [HarmonyPatch(typeof(ABI_RC.Core.CVRDecrypt))]
        class DecryptPatch
        {
            static readonly FastCVRDecrypt fastCVRDecrypt = new FastCVRDecrypt();
            [HarmonyPrefix]
            [HarmonyPatch(nameof(ABI_RC.Core.CVRDecrypt.Decrypt))]
            static bool Prefix(out byte[] __result, string guid, byte[] bytes, byte[] keyFrag) 
            {
                __result = null;
                if (!enableMod.Value)
                    return true;
                var data = new DownloadData(guid, bytes, keyFrag);
                FileCache.Decrypt(data);
                while (!data.DecryptDone && !data.DecryptFailed)
                {
                    Thread.Sleep(1);
                }
                if (data.DecryptFailed)
                    return false;
                __result = data.decryptedData;
                //__result = fastCVRDecrypt.Decrypt(guid, bytes, keyFrag);
                return false;
            }
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
                var key = StringToLongHash(task.FileKey);
                fileIds[key] = task.FileId;
                var cacheKey = new CacheKey(objectId, task.FileId, key);
                if (t == DownloadTask.ObjectType.Avatar)
                    playerIdToAsset[instTarget] = cacheKey;
                MelonCoroutines.Start(LoadPatch(b, objectId, t, tags, instTarget, task.FileId, job: task, cacheKey: cacheKey));
            }
        }
        [HarmonyPatch(typeof(CVRPlayerManager))]
        class PlayerManagerPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(CVRPlayerManager.ClearPlayerAvatars))]
            static bool ClearPlayerAvatarsPrefix(CVRPlayerManager __instance)
            {
                if (!enableMod.Value)
                    return true; 
                foreach (var download in downloadTasks)
                {
                    download.Value.cancellationToken.Cancel();
                }
                foreach (var asset in cachedAssets)
                {
                    foreach (var instance in asset.Value.Instances)
                    {
                        asset.Value.RemoveInstanceInternal(instance);
                    }
                }
                foreach (var player in __instance.NetworkPlayers)
                {
                    if (string.IsNullOrEmpty(player.Uuid))
                        continue;
                    CVRDownloadManager.Instance.RemoveQueuedUser(player.Uuid);
                    var puppetMaster = player.PuppetMaster;
                    if (!puppetMaster)
                        puppetMaster = player.PlayerObject.GetComponent<PuppetMaster>();
                    if (!puppetMaster || puppetMaster._isBlocked) 
                        continue;
                    puppetMaster._isBlocked = true;
                    puppetMaster.AvatarDestroyed();
                    CacheItem.ClearTransformChildren(player.AvatarHolder);
                    MelonCoroutines.Start(LoadPatch(null, null, DownloadTask.ObjectType.Avatar, null, player.Uuid, null, null));
                }
                return false;
            }
        }


        public static void UpdateLoadingAvatars()
        {
            if (loadingTasks.Count == 0)
                return;
            foreach (var item in loadingTasks)
            {
                if (item.Value.DownloadData.status == DownloadData.Status.Done)
                    continue;
                item.Value.UpdateLoadingAvatar();
            }
        }
        private static void UpdateBlockedAvatar()
        {
            playerIdToAsset.Clear();
            try
            {
                FileCache.InitDirectoryNames();
                cachedAssets.Remove(BlockedKey);
                cachedAssets[BlockedKey] = new CacheItem(_BLOCKED_NAME, _BLOCKED_VERSION, AssetType.Avatar, MetaPort.Instance?.blockedAvatarPrefab, new Tags(), _BLOCKED_NAME, true);
            }
            catch (Exception e)
            {
                MelonLogger.Msg(e.Message + e.StackTrace, e);
            }
        }
        public static void CleanupCache()
        {
            idsToRemove.Clear();
            var keys = cachedAssets.Keys.AsEnumerable();
            var now = DateTime.UtcNow;
            byte maxLifeTimeMinutes = FlushCache ? (byte)0 : maxLifeTimeMinutesEntry.Value;
            var maxAge = TimeSpan.FromMinutes(maxLifeTimeMinutes);
            foreach (var id in keys)
            {
                var item = cachedAssets[id];
                if (item.CanRemove(maxAge, now, out var age))
                    idsToRemove.Add(id, age);
            }
            keys = downloadTasks.Keys.AsEnumerable();
            foreach (var id in keys)
            {
                var item = downloadTasks[id];
                if (item.Failed)
                    idsToRemove.Add(id, TimeSpan.MaxValue);
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
                var key = item.Key;
                if (cachedAssets.TryGetValue(key, out var cacheItem))
                    cacheItem.Destroy();
                cachedAssets.Remove(key);
                downloadTasks.Remove(key);
                fileIds.Remove(key.KeyHash);
            }
            idsToRemove.Clear();
            if (enableGC.Value)
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

      
        private static bool StartDownloadAsset(CacheKey cacheKey, string assetId, DownloadTask.ObjectType type, string assetUrl, string fileId, long fileSize, string fileKey, string target, string fileHash,
                UgcTagsData tagsData, bool joinOnComplete, string thisDl)
        {
            if (!FileCache.InitDone)
                return true;
            MelonCoroutines.Start(DownloadAsset(cacheKey, assetId, type, assetUrl, fileId, fileSize, fileKey, target, fileHash, tagsData, joinOnComplete, thisDl));

            return false;
        }

        private static IEnumerator LoadExistingAsset(string target, CacheKey cacheKey, DownloadData downloadData, string thisDl) 
        {
            try
            {
                if (!loadingTasks.ContainsKey(target))
                {
                    loadingTasks[target] = new LoadTask(downloadData, FindPlayer(owner: target), IsLocal(target));
                }
                yield return new WaitForEndOfFrame();

                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Found existing download task for '{downloadData.type}', '{downloadData.assetId}'.");
                yield return WaitForDownloadToComplete(downloadData);
                if (!downloadData.IsDone || downloadData.Failed)
                    yield break;
                if (cachedAssets.TryGetValue(cacheKey, out var item))
                {
                    if (enableLog.Value)
                        MelonLogger.Msg($"[DL-{thisDl}] Loading asset '{downloadData.type}', '{downloadData.assetId}' from cache");
                    MelonCoroutines.Start(InstantiateItem(item, downloadData.objectType, downloadData.assetId, downloadData.fileId, target, thisDl, cacheKey));
                }
                else
                {
                    if (enableLog.Value)
                        MelonLogger.Error($"[DL-{thisDl}] Loading asset '{downloadData.type}', '{downloadData.assetId}' from cache failed.");
                }
            }
            finally
            {
                loadingTasks.Remove(target);
            }

        }

        private static void LogDownloadStats(DownloadData data, string thisDl)
        {
            if (!enableLog.Value)
                return;
            MelonLogger.Msg($"[DL-{thisDl}] Asset loading ended for '{data.type}' with ID '{data.assetId}', success {!data.Failed}. {data.fileSize / 1024f / 1024f} MB \r\n" +
                $"DownloadTime: {data.DownloadTime.TotalMilliseconds} ms, FileReadTime: {data.FileReadTime.TotalMilliseconds} ms, MD5HashTime: {data.MD5HashTime.TotalMilliseconds} ms, VerifyTime: {data.VerifyTime.TotalMilliseconds} ms, DecryptTime: {data.DecryptTime.TotalMilliseconds} ms.");
        }

        private static IEnumerator DownloadAsset(CacheKey cacheKey, string assetId, DownloadTask.ObjectType type, string assetUrl, string fileId, long fileSize, string fileKey, string target, string fileHash,
                UgcTagsData tagsData, bool joinOnComplete, string thisDl)
        {
            if (downloadTasks.TryGetValue(cacheKey, out var downloadData))
            {
                yield return LoadExistingAsset(target, cacheKey, downloadData, thisDl);
                yield break;
            }
            if (enableLog.Value)
                MelonLogger.Msg($"[DL-{thisDl}] Starting loading asset '{type}' with ID '{assetId}'.");
            var assetType = type.ToAssetType();
            var filePath = FileCache.GetFileCachePath(assetType, assetId, fileId);
            var longHash = StringToLongHash(fileKey);
            var dlData = new DownloadData(assetId, type, assetUrl, fileId, fileSize, fileKey, fileHash, joinOnComplete, tagsData, target, filePath, CVRDownloadManager.Instance.MaxBandwidth);
            downloadTasks[cacheKey] = dlData;

            fileIds[longHash] = fileId;

            if (!loadingTasks.TryGetValue(target, out var task))
                task = loadingTasks[target] = new LoadTask(dlData, FindPlayer(owner: target), IsLocal(target));

            yield return endOfFrame;
            if (enableGC.Value)
            {
                GarbageCollector.CollectIncremental(GarbageCollector.incrementalTimeSliceNanoseconds);
                yield return endOfFrame;
            }

            // limit maximum number of concurrent downloads
            // downloadSemaphore
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            int timeout = loadTimeoutSecondsEntry.Value;
            while (downloadSemaphore.CurrentCount == 0)
            {
                if (timeout == 0 || sw.Elapsed.TotalSeconds < timeout)
                    yield return null;
                else
                {
                    MelonLogger.Error($"[DL-{thisDl}] Timeout downloading asset {type}: ID: '{assetId}', fileId: '{fileId}', target: '{target}'.");
                    loadingTasks.Remove(target);
                    yield break;
                }
            }
            while (!downloadSemaphore.Wait(0))
                yield return null;
            try
            {
                bool download = false;
                var exists = FileCache.FileExists(assetType, assetId, fileId);

                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Cached file exists: {exists}, CheckDiskCacheHash: {checkHash.Value}, fileHash: '{fileHash}' asset '{type}' with ID '{assetId}'.");

                if ((exists == FileCache.FileCheck.ExistsSameVersion && !string.IsNullOrEmpty(fileHash)) || (exists == FileCache.FileCheck.ExistsDifferentVersion && string.IsNullOrEmpty(assetUrl)))
                {
                    yield return ReadRawBytesWait(dlData, thisDl);

                    if (dlData.FileReadFailed)
                        download = true;

                    if (enableLog.Value)
                        MelonLogger.Msg($"[DL-{thisDl}] Cached file loaded: {dlData.FileReadDone} asset '{type}' with ID '{assetId}'.");

                    if (checkHash.Value && !string.IsNullOrEmpty(fileHash))
                        yield return HashRawBytesWait(dlData, thisDl);
                    else
                        dlData.HashDone = true;

                    if (checkHash.Value && !string.Equals(fileHash, dlData.calculatedHash))
                        download = true;    // wrong hash on disk, download again

                    if (enableLog.Value)
                        MelonLogger.Msg($"[DL-{thisDl}] Cached file hash check enabled: {checkHash.Value}, done: {dlData.HashDone}, asset '{type}' with ID '{assetId}'.");
                }
                else
                {
                    download = true;
                }
                if (download)
                {
                    if (enableLog.Value)
                        MelonLogger.Msg($"[DL-{thisDl}] Starting downloading asset '{type}' with ID '{assetId}'. ");
                    yield return StartDownloadWait(dlData, thisDl);
                    if (dlData.FileReadFailed)
                    {
                        MelonLogger.Error($"[DL-{thisDl}] Error loading asset {type} (Download/read Failed): ID: '{assetId}', fileId: '{fileId}', owner: '{target}'.");
                        dlData.rawData = null;
                        dlData.decryptedData = null;
                        yield break;
                    }
                    if (enableLog.Value)
                        MelonLogger.Msg($"[DL-{thisDl}] Finished downloading asset '{type}' with ID '{assetId}'.");
                }
                if (dlData.HashFailed)
                {
                    MelonLogger.Error($"[DL-{thisDl}] Error loading asset {type} (Hash Failed): ID: '{assetId}', fileId: '{fileId}', owner: '{target}'.");
                    dlData.DecryptFailed = true;
                    dlData.rawData = null;
                    dlData.decryptedData = null;
                    yield break;
                }

                yield return DecryptRawBytesWait(dlData, thisDl);
                if (dlData.DecryptFailed)
                {
                    dlData.rawData = null;
                    dlData.decryptedData = null;
                    MelonLogger.Error($"[DL-{thisDl}] Error loading asset {type} (Decrypt Failed): ID: '{assetId}', fileId: '{fileId}', owner: '{target}'.");
                    yield break;
                }
                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Cached file decrypt: {dlData.DecryptDone} asset '{type}' with ID '{assetId}'. ");

                yield return VerifyWait(dlData, thisDl);
                if (dlData.VerifyFailed || !dlData.Verified)
                {
                    dlData.rawData = null;
                    dlData.decryptedData = null;
                    MelonLogger.Error($"[DL-{thisDl}] Error loading asset {type} (Verify Failed): ID: '{assetId}', fileId: '{fileId}', owner: '{target}'.");
                    yield break;
                }

                dlData.ReadyToInstantiate = true;

                if (download)
                    FileCache.WriteToDisk(dlData);
                else
                    dlData.rawData = null;
                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Loading finished for asset '{type}' with ID '{assetId}'. ");
                if (dlData.cancellationToken.IsCancellationRequested)
                {
                    MelonLogger.Msg($"[DL-{thisDl}] Loading cancelled for asset '{type}' with ID '{assetId}'. ");
                    yield break;
                }
                yield return LoadPatch(dlData.decryptedData, assetId, type, new Tags(tagsData), target, fileId, downloadData: dlData, thisDl: thisDl, cacheKey: cacheKey);
            }
            finally 
            {
                downloadSemaphore.Release();
                task.UpdateLoadingAvatar();
                LogDownloadStats(dlData, thisDl);
                loadingTasks.Remove(target);
            }
        }

        private static IEnumerator WaitForDownloadToComplete(DownloadData downloadData)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            int timeout = loadTimeoutSecondsEntry.Value;
            bool noTimeout = timeout == 0;
            timeout = Mathf.Max(timeout, 5);
            while (!downloadData.IsDone && !downloadData.Failed)
            {
                if (noTimeout || sw.Elapsed.TotalSeconds < timeout)
                    yield return new WaitForSeconds(0.5f);
                else
                    yield break;
            }
        }

        private static IEnumerator VerifyWait(DownloadData dlData, string thisDl)
        {
            if (!FileCache.ShouldVerify(dlData))
                yield break;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            dlData.status = DownloadData.Status.BundleVerifierQueued;
            FileCache.Verify(dlData);
            int timeout = loadTimeoutSecondsEntry.Value;
            bool noTimeout = timeout == 0;
            timeout = Mathf.Max(timeout, 5);
            while (!dlData.VerifyDone && !dlData.VerifyFailed)
            {
                if (noTimeout || sw.Elapsed.TotalSeconds < timeout)
                    yield return null;
                else
                {
                    dlData.VerifyFailed = true;
                    MelonLogger.Error($"[DL-{thisDl}] Timeout loading asset {dlData.type}: Verify timeout. ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                    yield break;
                }
            }
            if (dlData.VerifyFailed)
            {
                MelonLogger.Error($"[DL-{thisDl}] Error loading asset {dlData.type}: Verify Failed.  ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                yield break;
            }
        }


        private static IEnumerator DecryptRawBytesWait(DownloadData dlData, string thisDl)
        {
            var sw = new System.Diagnostics.Stopwatch();
            dlData.status = DownloadData.Status.DecryptingQueued;
            FileCache.Decrypt(dlData);
            sw.Start();

            while (!dlData.DecryptDone && !dlData.DecryptFailed)
            {
                if (loadTimeoutSecondsEntry.Value == 0 || sw.Elapsed.TotalSeconds < loadTimeoutSecondsEntry.Value)
                    yield return null;
                else
                {
                    dlData.DecryptFailed = true;
                    MelonLogger.Error($"[DL-{thisDl}] Timeout loading asset {dlData.type}: Decrypt timeout. ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                    yield break;
                }
            }
            if (dlData.DecryptFailed)
            {
                MelonLogger.Error($"[DL-{thisDl}] Error loading asset {dlData.type}: Decrypt Failed.  ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                yield break;
            }
        }

        private static IEnumerator ReadRawBytesWait(DownloadData dlData, string thisDl)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            dlData.status = DownloadData.Status.LoadingFromFileQueued;
            FileCache.ReadFile(dlData);
            while (!dlData.FileReadDone && !dlData.FileReadFailed)
            {
                if (loadTimeoutSecondsEntry.Value == 0 || sw.Elapsed.TotalSeconds < loadTimeoutSecondsEntry.Value)
                    yield return null;
                else
                {
                    dlData.FileReadFailed = true;
                    MelonLogger.Error($"[DL-{thisDl}] Timeout loading asset {dlData.type} from disk: ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                    yield break;
                }
            }
            if (dlData.FileReadFailed)
            {
                MelonLogger.Error($"[DL-{thisDl}] Error loading asset {dlData.type} from disk: ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                yield break;
            }
            sw.Stop();
        }

        private static IEnumerator HashRawBytesWait(DownloadData dlData, string thisDl) 
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            dlData.status = DownloadData.Status.HashingQueued;
            FileCache.Hash(dlData);
            while (!dlData.HashDone && !dlData.HashFailed)
            {
                if (loadTimeoutSecondsEntry.Value == 0 || sw.Elapsed.TotalSeconds < loadTimeoutSecondsEntry.Value)
                    yield return null;
                else
                {
                    dlData.HashFailed = true;
                    MelonLogger.Error($"[DL-{thisDl}] Timeout loading asset {dlData.type}: hash timeout. ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                    yield break;
                }
            }
            if (dlData.HashFailed)
            {
                MelonLogger.Error($"[DL-{thisDl}] Error loading asset {dlData.type}: hash failed. expected: '{dlData.fileHash}' actual: '{dlData.calculatedHash}' ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                yield break;
            }
            sw.Stop();
        }

        private static readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        private static IEnumerator StartDownloadWait(DownloadData dlData, string thisDl)
        {
            int timeout = loadTimeoutSecondsEntry.Value;
            bool noTimeout = timeout == 0;
            timeout = Mathf.Max(timeout, 5);
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            FileCache.Download(dlData);
            while (!dlData.DownloadDone)
            {
                if (noTimeout || sw.Elapsed.TotalSeconds < timeout)
                    yield return null;
                else
                {
                    dlData.FileReadFailed = true;
                    MelonLogger.Error($"[DL-{thisDl}] Timeout loading asset {dlData.type}: download timeout. ID: '{dlData.assetId}', fileId: '{dlData.fileId}', owner: '{dlData.target}'.");
                    yield break;
                }
            }
        }

        private static ulong StringToLongHash(string text) 
        {
            ulong value = 0;
            unchecked
            {
                ulong prime = 1;
                for (int i = 0; i < text.Length; i++)
                {
                    value += text[i] * prime;
                    prime *= 97;
                }
            }
            return value;
        }
        private static readonly System.Diagnostics.Stopwatch AnimatorStopwatch = new System.Diagnostics.Stopwatch();
        private static IEnumerator InstantiateItem(CacheItem item, DownloadTask.ObjectType type, string assetId, string fileId, string owner, string thisDl, CacheKey cacheKey, bool wait = true)
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
                        MelonLogger.Error($"[DL-{thisDl}] Timeout loading asset {type}: ID: '{assetId}', fileId: '{fileId}', owner: '{owner}'.");
                        yield break;
                    }
                } 
                while (!instantiateSemaphore.Wait(0)) 
                    yield return null;
            }
            try
            {
                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Instantiating item {type}: ID: '{assetId}', fileId: '{fileId}', owner: '{owner}'.");
                GameObject parent;
                GameObject instance = null;
                var instanceList = new List<GameObject>(1);
                if (type == DownloadTask.ObjectType.Avatar)
                {
                    bool local = IsLocal(owner);
                    CVRPlayerEntity player = FindPlayerParent(owner, out parent, local);
                    yield return InstantiateAvatar(item, instanceList, assetId, owner, parent, player, local, thisDl, cacheKey);
                    instance = instanceList.Count == 0 ? null : instanceList[0];
                    if (!instance)
                    {
                        if (playerIdToAsset.TryGetValue(owner, out var key) && key != cacheKey)
                            yield break;
                        MelonLogger.Error($"[DL-{thisDl}] Instantiating item {type} failed: ID: '{assetId}', owner: '{owner}'.");
                        yield break;
                    }
                    if (player != null && player.LoadingAvatar != null)
                        GameObject.DestroyImmediate(player.LoadingAvatar);
                    //    player.LoadingAvatar.gameObject.SetActive(false);
                    ActivateInstance(item, parent, instance);
                    SetupInstantiatedAvatar(instance, player, thisDl);
                    if (local)
                    {
                        CVR_MenuManager.Instance.UpdateMenuPosition();
                        ViewManager.Instance.UpdateMenuPosition(true);
                    }
                    yield return null;
                    if (enableLog.Value)
                    {
                        if (instance.TryGetComponent<Animator>(out var animator))
                        {
                            AnimatorStopwatch.Restart();
                            animator.Update(0f);
                            AnimatorStopwatch.Stop();
                            MelonLogger.Msg($"[DL-{thisDl}] Animator exectuion time for ID '{assetId}': {AnimatorStopwatch.Elapsed.TotalMilliseconds} ms. Layer count: {animator.layerCount}. ");
                        }
                    }
                }
                else if (type == DownloadTask.ObjectType.Prop)
                {
                    var propData = FindProp(owner);
                    if (propData == null)
                    {
                        MelonLogger.Warning($"[DL-{thisDl}] Instantiating item {type} failed: ID: '{assetId}', owner: '{owner}'. Prop data not found. Deleted before loading completed? Prop Count: { (CVRSyncHelper.Props?.Count.ToString() ?? "-null-")}'.");
                        yield break;
                    }
                    parent = new GameObject(owner);
                    yield return InstantiateProp(item, instanceList, owner, parent, propData, thisDl);
                    instance = instanceList.Count == 0 ? null : instanceList[0];
                    if (!instance)
                    {
                        MelonLogger.Error($"[DL-{thisDl}] Instantiating item {type} failed: ID: '{assetId}', owner: '{owner}', instances.Count: '{instanceList.Count}'.");
                        yield break;
                    }
                    yield return null;
                    ActivateInstance(item, parent, instance);
                    SetPropData(propData, parent, instance, thisDl);
                }
                if (enablePerfStat.Value)
                {
                    MelonCoroutines.Start(item.PerfCheck(instance));
                }
                if (wait)
                {
                    yield return null;
                }
            }
            finally
            {
                if (wait)
                    instantiateSemaphore.Release();

            }
        }

        internal static bool IsLocal(string name) => string.Equals(name, "_PLAYERLOCAL") || string.Equals(name, MetaPort.Instance.ownerId);

        private static CVRPlayerEntity FindPlayerParent(string owner, out GameObject parent, bool local)
        {
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
            return player;
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
        private static IEnumerator InstantiateAvatar(CacheItem item, List<GameObject> instances, string assetId, string owner, GameObject parent, CVRPlayerEntity player, bool isLocal, string thisDl, CacheKey cacheKey)
        {
            var oldGameObjectCount = parent.transform.childCount;
            var oldGameObjects = new GameObject[oldGameObjectCount];
            for (int i = 0; i < oldGameObjectCount; i++)
                oldGameObjects[i] = parent.transform.GetChild(i).gameObject;

            GameObject tempParent = new GameObject();
            tempParent.SetActive(false);
            tempParent.transform.SetParent(parent.transform);
            GameObject instance;
            if (isLocal)
            {
                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Local avatar: owner: '{owner}', MetaPort.Instance.ownerId: {MetaPort.Instance.ownerId}.");
                yield return item.GetSanitizedAsset(tempParent, DownloadTask.ObjectType.Avatar, instances, item.Tags, assetId, isLocal: true);
                if (instances.Count == 0 || !instances[0])
                {
                    MelonLogger.Error($"[DL-{thisDl}] Instantiating avatar failed: ID: '{assetId}', owner: local.");
                    yield break;
                }
                yield return null;
                yield return new WaitForEndOfFrame();
                instance = instances[0];
                if (playerIdToAsset.TryGetValue(owner, out var _key) && _key != cacheKey)
                {
                    GameObject.DestroyImmediate(instance);
                    MelonLogger.Warning($"[DL-{thisDl}] (InstantiateAvatar) Instantiating avatar cancelled: ID: '{assetId}', owner: '{owner}'.");
                    yield break;
                }
                MetaPort.Instance.currentAvatarGuid = assetId;
                PlayerSetup.Instance.ClearAvatar();
                ActivateNewGameObject(instance, parent, oldGameObjects, tempParent);
                yield break;
            }
            if (player == null || !player.AvatarHolder)
            {
                MelonLogger.Error($"[DL-{thisDl}] Cannot instantiate avatar: player not found. id: '{owner}'.");
                yield break;
            }
            if (player.LoadingAvatar && player.LoadingAvatar.task != null)
                player.LoadingAvatar.task.Status = DownloadTask.ExecutionStatus.Complete;
            if (!parent)
            {
                MelonLogger.Error($"[DL-{thisDl}] Cannot instantiate avatar: avatar holder not found. id: '{owner}'.");
                yield break;
            }
            bool friendsWith = FriendsWith(owner);
            bool avatarVisibility = MetaPort.Instance.SelfModerationManager.GetAvatarVisibility(owner, assetId, out bool forceHidden, out bool forceShow);
            string blockReason = string.Empty;
            avatarVisibility = avatarVisibility && ABI_RC.Core.Util.AssetFiltering.AssetFilter.GetAvatarVisibility(ref blockReason, owner, item.Tags.AvatarTags, friendsWith, forceShow, forceHidden, null);
            if (enableLog.Value && !string.IsNullOrEmpty(blockReason))
                MelonLogger.Msg($"[DL-{thisDl}] Avatar hidden, reason: {blockReason}. ID: {assetId}, owner ID: {owner}");
            forceShow = forceShow && avatarVisibility;
            forceHidden = forceHidden && !avatarVisibility;
            yield return item.GetSanitizedAsset(tempParent, DownloadTask.ObjectType.Avatar, instances, item.Tags, assetId, isLocal: false, friendsWith: friendsWith, isVisible: avatarVisibility, forceShow: forceShow, forceBlock: forceHidden);
            if (instances.Count == 0 || !instances[0])
            {
                MelonLogger.Error($"[DL-{thisDl}] Instantiating avatar failed: ID: '{assetId}', owner: '{owner}'.");
                yield break;
            }
            yield return null;
            yield return new WaitForEndOfFrame();
            //delete current avatar, disable parent, move new to proper parent
            if (instances.Count == 0 || !instances[0])
            {
                MelonLogger.Error($"[DL-{thisDl}] Instantiating avatar failed: ID: '{assetId}', owner: '{owner}'.");
                yield break;
            }
            instance = instances[0];

            if (playerIdToAsset.TryGetValue(owner, out var key) && key != cacheKey)
            {
                GameObject.DestroyImmediate(instance);
                MelonLogger.Warning($"[DL-{thisDl}] (InstantiateAvatar) Instantiating avatar cancelled: ID: '{assetId}', owner: '{owner}'.");
                yield break;
            }
            if (!parent)
            {
                FindPlayerParent(owner, out parent, isLocal);
                if (!parent)
                {
                    MelonLogger.Error($"[DL-{thisDl}] Instantiating avatar failed, owner '{owner}' cannot be found.");
                    CacheItem.RemoveInstance(instance);
                    GameObject.DestroyImmediate(instance);
                    yield break;
                }
            }
            if (enableLog.Value)
                MelonLogger.Msg($"[DL-{thisDl}] Activating avatar.");
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

        private static IEnumerator InstantiateProp(CacheItem item, List<GameObject> instanceList, string target, GameObject parent, CVRSyncHelper.PropData propData, string thisDl)
        {
            if (propData == null || item == null)
            {
                if (enableLog.Value) 
                    MelonLogger.Error($"[DL-{thisDl}] InstantiateProp failed, propData '{propData == null}', item: '{item == null}'.");
                yield break;
            }
            parent.transform.SetPositionAndRotation(new Vector3(propData.PositionX, propData.PositionY, propData.PositionZ),
                Quaternion.Euler(propData.RotationX, propData.RotationY, propData.RotationZ));
            yield return item.GetSanitizedProp(parent, instanceList, item.Tags, item.AssetId, target, propData.SpawnedBy);
        }
        private static void SetPropData(CVRSyncHelper.PropData propData, GameObject container, GameObject propInstance, string thisDl)
        {
            if (!propInstance)
                return;
            if (!propInstance.TryGetComponent<CVRSpawnable>(out var component) || component.transform == null)
            {
                MelonLogger.Error($"[DL-{thisDl}] SetPropData failed, component found? '{component == null}'.");
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
            if (!cvrplayerEntity.PlayerObject.TryGetComponent<CapsuleCollider>(out var playerCapsule) || 
                !instance.TryGetComponent<CVRAvatar>(out var cvrAvatar))
                return;
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

       
        private static void SetupInstantiatedAvatar(GameObject instance, CVRPlayerEntity player, string thisDl)
        {
            if (player != null)
            {
                var puppetMaster = player.PuppetMaster;
                if (!puppetMaster)
                {
                    MelonLogger.Error($"[DL-{thisDl}] PuppetMaster is null for {player.PlayerNameplate}, {player.Username}, {player.Uuid}");
                    return;
                }
                puppetMaster.avatarObject = instance;
                puppetMaster.AvatarInstantiated();
            }
            else
                PlayerSetup.Instance.SetupAvatar(instance);
        }
        private static IEnumerator LoadPatch(byte[] bytes, string assetId, DownloadTask.ObjectType type, Tags tags, string owner, string FileId, CacheKey cacheKey, DownloadTask job = null, DownloadData downloadData = null, string thisDl = "0")
        {
            var assetPath = type == DownloadTask.ObjectType.Avatar ? _AVATAR_PATH : _PROP_PATH;
            if (bytes == null && type == DownloadTask.ObjectType.Avatar)
            {
                if (downloadData != null)
                    downloadData.status = DownloadData.Status.LoadingBundle;
                var item = cachedAssets[BlockedKey];
                yield return InstantiateItem(item, type, item.AssetId, item.FileId, owner, "----", cacheKey);
                if (job != null)
                    job.Status = DownloadTask.ExecutionStatus.Complete;
                if (downloadData != null)
                    downloadData.status = DownloadData.Status.Done;
                yield break;
            }

            if (downloadData != null)
                downloadData.status = DownloadData.Status.LoadingBundleQueued;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var timeout = loadTimeoutSecondsEntry.Value;
            while (bundleLoadSemaphore.CurrentCount == 0)
            {
                if (timeout == 0 || sw.Elapsed.TotalSeconds < timeout)
                    yield return null;
                else
                    yield break;
            }
            while (!bundleLoadSemaphore.Wait(0))
                yield return null;
            if (downloadData != null)
                downloadData.status = DownloadData.Status.LoadingBundle;
            bool shouldRelease = true;
            bool shouldUnload = true;
            AssetBundleCreateRequest bundle = null;
            try
            {
                bundle = AssetBundle.LoadFromMemoryAsync(bytes);
                yield return bundle;
                if (bundle == null || !bundle.assetBundle ||
                    type == DownloadTask.ObjectType.Avatar && !IsLocal(owner) && !CVRPlayerManager.Instance.TryGetConnected(owner) ||
                    !GetAssetName(assetPath, bundle.assetBundle.GetAllAssetNames(), out string assetName, ref shouldUnload, thisDl))
                {
                    if (downloadData != null)
                        downloadData.status = DownloadData.Status.Error;
                    yield break;
                }
                AssetBundleRequest asyncAsset;
                yield return asyncAsset = bundle.assetBundle.LoadAssetAsync(assetName, typeof(GameObject));
                var gameObject = (GameObject)asyncAsset.asset;
                if ((bundle?.assetBundle) != null && bundle != null)
                {
                    if (shouldUnload)
                        bundle.assetBundle.Unload(false);
                    bundleLoadSemaphore.Release();
                    shouldRelease = false;
                }
                var name = GetName(gameObject, FileId);
                var item = cachedAssets[cacheKey] = new CacheItem(assetId, FileId, type.ToAssetType(), gameObject, tags, name, assetName, shouldUnload ? null: bundle.assetBundle);
                if (enableLog.Value)
                    MelonLogger.Msg($"[DL-{thisDl}] Added asset '{type}' with ID '{assetId}', file ID '{FileId}', game object name '{(gameObject != null ? gameObject.name : "-null-")}', tags: {tags}.");
                yield return InstantiateItem(item, type, assetId, FileId, owner, thisDl, cacheKey, wait: false);
            }
            finally
            {
                if (shouldUnload && (bundle?.assetBundle) != null && bundle != null)
                    bundle.assetBundle.Unload(false);
                if (shouldRelease)
                    bundleLoadSemaphore.Release();
            }
            if (job != null)
                job.Status = DownloadTask.ExecutionStatus.Complete;
            if (downloadData != null)
            {
                downloadData.status = DownloadData.Status.Done;
                downloadData.IsDone = true;
            }
            yield break;
        }

        private static bool GetAssetName(string expectedName, string[] allAssetNames, out string assetName, ref bool shouldUnload, string thisDl)
        {
            if (allAssetNames == null || allAssetNames.Length == 0)
            {
                MelonLogger.Warning($"[DL-{thisDl}] Bundle has no assets in it!");
                assetName = null;
                return false;
            }
            assetName = allAssetNames[0];
            if (allAssetNames.Length > 1)
            {
                MelonLogger.Warning($"[DL-{thisDl}] Bundle has multiple assets in it! Names: { string.Join(" ,", allAssetNames) }. Only the first one will be used!");
            }
            else if (!string.Equals(assetName, expectedName))
            {
                shouldUnload = assetName.Length < 50; // placeholder check
                MelonLogger.Warning($"[DL-{thisDl}] Bundle's asset name is unexpected! { assetName }. It will be loaded anyway, but this would not work normally!");
            }
            return true;
        }
        internal static int GetAllLoadedAssetBundlesCount() 
        {
            var bundles = AssetBundle.GetAllLoadedAssetBundles();
            return bundles.Count();
        }
        internal static string GetAllLoadedAssetBundlesNames()
        {
            var bundles = AssetBundle.GetAllLoadedAssetBundles();
            List<string> names = new List<string>(bundles.Count());
            foreach (var bundle in bundles)
            {
                names.Add(bundle.name);
            }
            return string.Join(", ", names);
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