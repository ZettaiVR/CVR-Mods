using ABI_RC.Core;
using ABI_RC.Core.IO;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zettai
{
    public class CacheItem
    {
        public CacheItem(string id, string objectFileId, DownloadJob.ObjectType type, GameObject item, Tags tags, string name)
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
            FileId = objectFileId;
        }
        internal CacheItem(string id, string objectFileId, DownloadJob.ObjectType type, GameObject item, Tags tags, string name, bool readOnly)
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
            FileId = objectFileId;
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
        public DownloadJob.ObjectType ObjectType { get; }
        public Tags Tags { get; }
        public DateTime AddTime { get; }
        public int InstanceCount => instances.Count;
        public bool CanRemove(TimeSpan maxAge, DateTime now, out TimeSpan age)
        {
            age = now - lastRefRemoved;
            return !ReadOnly && InstanceCount == 0 && age > maxAge;
        }
        public GameObject GetSanitizedAvatar(GameObject parent, Tags tags, bool isLocal, bool friendsWith = false, bool forceShow = false, bool forceBlock = false) 
        {
            ClearTransformChildren(parent);
            parent.SetActive(false);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            if (!ReadOnly)
            {
                if (MemoryCache.enableOwnSanitizer.Value)
                {
                    if (isLocal)
                        Sanitizer.CleanAvatarGameObject(instance, tags);
                    else
                        Sanitizer.CleanAvatarGameObjectNetwork(instance, friendsWith, tags, forceShow, forceBlock);
                }
                else 
                {
                    if (isLocal)
                        CVRTools.CleanAvatarGameObject(instance, tags.AvatarTags);
                    else
                        CVRTools.CleanAvatarGameObjectNetwork(instance, friendsWith, tags.AvatarTags, forceShow, forceBlock);
                }
                SetAudioMixer(instance);
                AddInstance(instance);
            }
            return instance;
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

        public GameObject GetSanitizedProp(GameObject parent, Tags tags, bool isOwnOrFriend, bool? visibility)
        {
            ClearTransformChildren(parent);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            bool forceBlock = visibility == false;
            bool forceShow = visibility == true;
            CVRTools.CleanPropGameObjectNetwork(instance, isOwnOrFriend, tags.PropTags, false, forceShow, forceBlock, false);
            AddInstance(instance);
            return instance;
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
        public bool IsMatch(DownloadJob.ObjectType type, string id, string fileID) => 
            type == ObjectType &&
            string.Equals(id, AssetId) &&
            (string.IsNullOrEmpty(fileID) || string.Equals(fileID, FileId));
        internal static void ClearTransformChildren(GameObject playerAvatarParent)
        {
            if (playerAvatarParent.transform.childCount > 0)
                foreach (Transform tr in playerAvatarParent.transform)
                    UnityEngine.Object.DestroyImmediate(tr.gameObject, true);
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
