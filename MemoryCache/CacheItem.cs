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
        public CacheItem(string id, string FileId, DownloadTask.ObjectType type, GameObject item, Tags tags, string name)
        {
            AddTime = DateTime.UtcNow;
            AssetId = id;
            ObjectType = type;
            OriginalItem = item;
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            WasEnabled = item.activeSelf;
            item.SetActive(false);
            NormalizeQuaternionAll(item.transform);
            Name = name;
            Tags = tags;
            this.FileId = FileId;
        }
        internal CacheItem(string id, string FileId, DownloadTask.ObjectType type, GameObject item, Tags tags, string name, bool readOnly)
        {
            AddTime = DateTime.UtcNow;
            AssetId = id;
            ObjectType = type;
            OriginalItem = item;
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            WasEnabled = item.activeSelf;
            item.SetActive(false);
            NormalizeQuaternionAll(item.transform);
            ReadOnly = readOnly;
            Name = name;
            Tags = tags;
            this.FileId = FileId;
        }
        public override string ToString() => string.IsNullOrEmpty(Name) ? AssetId : Name;
        private readonly GameObject OriginalItem;
        private readonly HashSet<GameObject> instances = new HashSet<GameObject>();
        private DateTime lastRefRemoved;
        public bool WasEnabled { get; }
        public bool ReadOnly { get; }
        public string AssetId { get; }
        public string FileId { get; }
        public string Name { get; }
        public DownloadTask.ObjectType ObjectType { get; }
        public Tags Tags { get; }
        public DateTime AddTime { get; }
        public int InstanceCount => instances.Count;
        public bool CanRemove(TimeSpan maxAge, DateTime now, out TimeSpan age)
        {
            age = now - lastRefRemoved;
            return !ReadOnly && InstanceCount == 0 && age > maxAge;
        }
        public IEnumerator GetSanitizedAvatar(GameObject parent, List<GameObject> instances, Tags tags, string assetId, 
            bool isLocal, bool friendsWith = false, bool isVisible = false,
            bool forceShow = false, bool forceBlock = false)
        {
            if (instances == null)
                yield break;
            yield return new WaitForEndOfFrame();
            GameObject instance = GameObject.Instantiate(OriginalItem, parent.transform);
            instances.Add(instance);
            if (ReadOnly)
            {
                yield return instance;
            }
            yield return new WaitForEndOfFrame();
            if (MemoryCache.enableOwnSanitizer.Value)
            {
                if (isLocal)
                    Sanitizer.CleanAvatarGameObject(instance, tags, assetId);
                else
                    Sanitizer.CleanAvatarGameObjectNetwork(instance, friendsWith, assetId, tags, forceShow, forceBlock);
            }
            else
            {
                if (isLocal)
                    CVRTools.CleanAvatarGameObject(assetId, instance, tags.AvatarTags, isVisible, forceShow, forceBlock);
                else
                    CVRTools.CleanAvatarGameObjectNetwork(assetId, instance, friendsWith, tags.AvatarTags, isVisible, forceShow, forceBlock);
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

        public IEnumerator GetSanitizedProp(GameObject parent, List<GameObject> instances, Tags tags, string assetId, bool isOwnOrFriend, bool visibility, bool wasForceHidden, bool wasForceShown)
        {
            ClearTransformChildren(parent);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            yield return null;
            CVRTools.CleanPropGameObjectNetwork(assetId, instance, isOwnOrFriend, tags.PropTags, visibility, wasForceShown, wasForceHidden, false);
            AddInstance(instance);
            instances.Add(instance);
        }
       
        private void AddInstance(GameObject item)
        {
            instances.Add(item);
            cacheInstances.Add(item, this);
        }
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
            var removed = instances.Remove(item);
            if (removed)
                lastRefRemoved = DateTime.UtcNow;
        }
        public bool HasInstance(GameObject item) => instances.Contains(item);
        internal void Destroy()
        {
            if (!ReadOnly)
                GameObject.DestroyImmediate(OriginalItem, true);
        }
        public bool IsMatch(DownloadTask.ObjectType type, string id, string fileID) => 
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
    }
}
