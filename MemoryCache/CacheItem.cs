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
            ReadOnly = readOnly;
            Name = name;
            Tags = tags;
            FileId = objectFileId;
        }
        public override string ToString() => string.IsNullOrEmpty(Name) ? AssetId : Name;
        private readonly GameObject OriginalItem;
        private readonly HashSet<GameObject> instances = new HashSet<GameObject>();
        public bool ReadOnly { get; }
        public string AssetId { get; }
        public string FileId { get; }
        public string Name { get; }
        public DownloadJob.ObjectType ObjectType { get; }
        public Tags Tags { get; }
        public DateTime AddTime { get; }
        public DateTime LastRefRemoved { get; private set; }
        public int InstanceCount => instances.Count; 
        public bool IsEmpty => InstanceCount == 0 && !ReadOnly;
        public GameObject GetSanitizedAvatar(GameObject parent, Tags tags, bool isLocal, bool friendsWith = false, bool forceShow = false, bool forceBlock = false) 
        {
            ClearTransformChildren(parent);
            parent.SetActive(false);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
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
                instances.Add(instance);
            }
            NormalizeQuaternionAll(parent.transform);
            //parent.SetActive(true);  // done later to split cpu load to multiple frames
            return instance;
        }
        static readonly List<Transform> transforms = new List<Transform>();
        private void NormalizeQuaternionAll(Transform transform)
        {
            transforms.Clear();
            transform.GetComponentsInChildren(true, transforms);
            for (int i = 0; i < transforms.Count; i++)
            {
                transforms[i].rotation.Normalize();
            }
            transforms.Clear();
        }

        public GameObject GetSanitizedProp(GameObject parent, Tags tags, bool isOwnOrFriend, bool? visibility)
        {
            ClearTransformChildren(parent);
            parent.SetActive(false);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;;
            bool forceBlock = visibility == false;
            bool forceShow = visibility == true;
            CVRTools.CleanPropGameObjectNetwork(instance, isOwnOrFriend, tags.PropTags, false, forceShow, forceBlock, false);
            NormalizeQuaternionAll(parent.transform);
            //parent.SetActive(true);
            instances.Add(instance);
            return instance;
        }
        public void RemoveInstance(GameObject item)
        {
            var removed = instances.Remove(item);
            if (removed)
                LastRefRemoved = DateTime.UtcNow;
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
            string.Equals(fileID, FileId);
        private static void ClearTransformChildren(GameObject playerAvatarParent)
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
        
    }
}
