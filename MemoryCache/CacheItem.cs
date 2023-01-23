using ABI_RC.Core;
using ABI_RC.Core.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zettai
{
    public class CacheItem
    {
        public CacheItem(string id, string fileId, AssetType type, GameObject item, Tags tags, string name, string assetName, bool readOnly, AssetBundle bundle)
        {
            AddTime = DateTime.UtcNow;
            AssetId = id;
            ObjectType = type;
            OriginalItem = item;
            WasEnabled = item.activeSelf;
            item.SetActive(false);
            NormalizeQuaternionAll(item.transform);
            Name = name;
            AssetName = assetName;
            Tags = tags;
            FileId = fileId;
            ReadOnly = readOnly;
            if (assetBundle != null)
            {
                assetBundle = bundle;
                HasBundle = true;
                if (!string.IsNullOrEmpty(assetName))
                    loadedAssetNames.Add(assetName);
            }
            if (type == AssetType.Prop)
                return;
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }
        internal CacheItem(string id, string FileId, AssetType type, GameObject item, Tags tags, string name) : this(id, FileId, type, item, tags, name, string.Empty, false, null) { }
        internal CacheItem(string id, string FileId, AssetType type, GameObject item, Tags tags, string name, bool readOnly) : this(id, FileId, type, item, tags, string.Empty, name, readOnly, null) { }
        internal CacheItem(string id, string FileId, AssetType type, GameObject item, Tags tags, string name, string assetName, AssetBundle assetBundle) : this(id, FileId, type, item, tags, assetName, name, false, assetBundle) { }
        public override string ToString() => string.IsNullOrEmpty(Name) ? AssetId : Name;
        private readonly AssetBundle assetBundle = null;
        private readonly GameObject OriginalItem;
        private readonly HashSet<GameObject> instances = new HashSet<GameObject>();
        private DateTime lastRefRemoved;
        public bool WasEnabled { get; }
        public bool ReadOnly { get; }
        public bool HasBundle { get; }
        public string AssetId { get; }
        public string FileId { get; }
        public string Name { get; }
        public string AssetName { get; }
        public AssetType ObjectType { get; }
        public Tags Tags { get; }
        public DateTime AddTime { get; }
        public int InstanceCount => instances.Count;
        public bool CanRemove(TimeSpan maxAge, DateTime now, out TimeSpan age)
        {
            age = now - lastRefRemoved;
            return !ReadOnly && InstanceCount == 0 && age > maxAge;
        }
        public IEnumerator GetSanitizedAsset(GameObject parent, DownloadTask.ObjectType type, List<GameObject> instancesList, Tags tags, string assetId, 
            bool isLocal, bool friendsWith = false, bool isVisible = true,
            bool forceShow = false, bool forceBlock = false)
        {
            if (instancesList == null || !OriginalItem)
                yield break;
            yield return new WaitForEndOfFrame();
            GameObject instance = GameObject.Instantiate(OriginalItem, parent.transform);
            instancesList.Add(instance);
            if (ReadOnly)
            {
                yield return instance;
            }
            yield return new WaitForEndOfFrame();
            if (MemoryCache.enableOwnSanitizer.Value)
            {
                if (type == DownloadTask.ObjectType.Avatar)
                {
                    if (isLocal)
                        Sanitizer.CleanAvatarGameObjectLocal(instance, tags);
                    else
                        Sanitizer.CleanAvatarGameObjectNetwork(instance, tags, friendsWith, forceShow, forceBlock, isVisible);
                }
                else if (type == DownloadTask.ObjectType.Prop) 
                {
                    Sanitizer.CleanPropGameObject(instance, tags, friendsWith, forceShow, forceBlock, isVisible);
                }
            }
            else
            {
                if (type == DownloadTask.ObjectType.Avatar)
                {
                    if (MemoryCache.enableLog.Value)
                        MelonLoader.MelonLogger.Msg($"assetId {assetId}, instance {instance}, layer {(isLocal ? 8 : 10)}, isFriend {isLocal || friendsWith}, isVisible {isVisible}, forceShow {forceShow}, forceBlock {forceBlock}");
                    ABI_RC.Core.Util.AssetFiltering.AssetFilter.FilterAvatar(assetId, instance, tags.AvatarTags, isLocal ? 8 : 10, isLocal || friendsWith, isVisible, forceShow, forceBlock);
                }
                else if (type == DownloadTask.ObjectType.Prop)
                    ABI_RC.Core.Util.AssetFiltering.AssetFilter.FilterProp(assetId, instance, tags.PropTags, isFriend: friendsWith, isVisible: isVisible, forceShow: forceShow, forceBlock: forceBlock);
            }
            SetAudioMixer(instance);
            AddInstance(instance);
        }
        static readonly List<Transform> transforms = new List<Transform>();
        private void NormalizeQuaternionAll(Transform transform)
        {
            transforms.Clear();
            transform.GetComponentsInChildren(true, transforms);
            foreach (Transform tr in transforms)
                tr.rotation.Normalize();
            transforms.Clear();
        }

        public IEnumerator GetSanitizedProp(GameObject parent, List<GameObject> instanceList, Tags tags, string assetId, string target, string spawnedBy)
        {
            if (instanceList == null || !OriginalItem)
                yield break;
            var empty = string.Empty;
            bool isOwnOrFriend = spawnedBy == ABI_RC.Core.Savior.MetaPort.Instance.ownerId || MemoryCache.FriendsWith(spawnedBy);
            bool isVisible = ABI_RC.Core.Savior.MetaPort.Instance.SelfModerationManager.GetPropVisibility(spawnedBy, target, out bool wasForceHidden, out bool wasForceShown);
            bool mustShow = string.Equals(spawnedBy, "SYSTEM") || string.Equals(spawnedBy, "LocalServer");
            if (MemoryCache.enableLog.Value)
                MelonLoader.MelonLogger.Msg($"isOwnOrFriend: {isOwnOrFriend}, isVisible: {isVisible}, mustShow: {mustShow}.");
            bool hidden = !mustShow && (!isVisible || !ABI_RC.Core.Util.AssetFiltering.AssetFilter.GetPropFilterStatus(ref empty, assetId, tags.PropTags, isOwnOrFriend, wasForceShown, wasForceHidden));
            if (hidden)
            {
                MelonLoader.MelonLogger.Error($"Hidden by content filter: '{empty}', Asset ID: {assetId}, target: {target}, spawned by: {spawnedBy}, isVisible: {isVisible}, isOwnOrFriend: {isOwnOrFriend}, wasForceShown: {wasForceShown}, wasForceHidden: {wasForceHidden}', tags: {tags}.");
                ABI_RC.Core.Util.AssetFiltering.AssetFilter.FilterProp(assetId, parent, tags.PropTags,isFriend: isOwnOrFriend, isVisible: isVisible, forceShow: wasForceShown, forceBlock: wasForceHidden);
                yield break;
            }
            ClearTransformChildren(parent);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            if (MemoryCache.enableLog.Value)
                MelonLoader.MelonLogger.Msg($"OriginalItem: '{(OriginalItem != null ? OriginalItem.name : "-null-")}', instance: '{(instance != null ? instance.name : "-null-")}'.");
            yield return null;
            if (!mustShow)
                ABI_RC.Core.Util.AssetFiltering.AssetFilter.FilterProp(assetId, instance, tags.PropTags, isFriend: isOwnOrFriend, isVisible: isVisible, forceShow: wasForceShown, forceBlock: wasForceHidden);
            else
                MelonLoader.MelonLogger.Warning($"Prop force shown regardless of content filter. Asset ID: {assetId}, target: {target}, spawned by: {spawnedBy}, isVisible: {isVisible}, isOwnOrFriend: {isOwnOrFriend}, wasForceShown: {wasForceShown}, wasForceHidden: {wasForceHidden}', tags: {tags}.");
            if (!instance)
                yield break;
            AddInstance(instance);
            instanceList.Add(instance);
        }
       
        private void AddInstance(GameObject item)
        {
            instances.Add(item);
            cacheInstances.Add(item, this);
        }
        public static bool AssetNameLoaded(string assetName) => loadedAssetNames.Contains(assetName);
        public static bool RemoveInstance(GameObject item) 
        {
            if (cacheInstances.TryGetValue(item, out var cacheItem)) 
            {
                cacheItem.RemoveInstanceInternal(item);
                return true;
            }
            return false;
        }
        private void RemoveInstanceInternal(GameObject item)
        {
            if (instances.Remove(item))
                lastRefRemoved = DateTime.UtcNow;
        }
        public bool HasInstance(GameObject item) => instances.Contains(item);
        internal void Destroy()
        {
            if (!string.IsNullOrEmpty(AssetName))
                loadedAssetNames.Remove(AssetName);
            if (!ReadOnly && OriginalItem)
                GameObject.DestroyImmediate(OriginalItem, true);
            if (HasBundle)
                assetBundle.Unload(true);
        }
        public bool IsMatch(AssetType type, string id, string fileID) => 
            type == ObjectType &&
            string.Equals(id, AssetId) &&
            (string.IsNullOrEmpty(fileID) || string.Equals(fileID, FileId));
        internal static void ClearTransformChildren(GameObject playerAvatarParent)
        {
            if (playerAvatarParent.transform.childCount > 0)
                foreach (Transform tr in playerAvatarParent.transform)
                    UnityEngine.Object.DestroyImmediate(tr.gameObject, true);
        }
        internal static void ClearTransformChildren(GameObject[] playerAvatarParent)
        {
            if (playerAvatarParent != null && playerAvatarParent.Length> 0)
                foreach (GameObject tr in playerAvatarParent)
                    UnityEngine.Object.DestroyImmediate(tr, true);
        }
        private static void SetAudioMixer(GameObject instance)
        {
            if (!instance)
                return;
            instance.GetComponentsInChildren(true, audioSources);
            var mixer = RootLogic.Instance.avatarSfx;
            for (int i = 0; i < audioSources.Count; i++)
                audioSources[i].outputAudioMixerGroup = mixer;
        }
        private static readonly List<AudioSource> audioSources = new List<AudioSource>();
        private static readonly Dictionary<GameObject, CacheItem> cacheInstances = new Dictionary<GameObject, CacheItem>();
        private static readonly HashSet<string> loadedAssetNames = new HashSet<string>();
    }
}
