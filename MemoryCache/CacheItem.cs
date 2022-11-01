using ABI_RC.Core;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking.API.Responses;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zettai
{
    public class CacheItem
    {
        public CacheItem(string id, DownloadJob.ObjectType type, GameObject item)
        {
            AddTime = DateTime.UtcNow;
            AssetId = id;
            ObjectType = type;
            OriginalItem = item;
        }
        internal CacheItem(string id, DownloadJob.ObjectType type, GameObject item, bool readOnly)
        {
            AddTime = DateTime.UtcNow;
            AssetId = id;
            ObjectType = type;
            OriginalItem = item;
            ReadOnly = readOnly;
        }
        private readonly GameObject OriginalItem;
        private readonly HashSet<GameObject> instances = new HashSet<GameObject>();
        public bool ReadOnly { get; }
        public string AssetId { get; }
        public DownloadJob.ObjectType ObjectType { get; }
        public DateTime AddTime { get; }
        public DateTime LastRefRemoved { get; private set; }
        public int InstanceCount => instances.Count; 
        public bool IsEmpty => InstanceCount == 0 && !ReadOnly;
        public GameObject GetSanitizedAvatar(GameObject parent, UgcTagsData tags, bool isLocal, bool friendsWith = false, bool forceShow = false, bool forceBlock = false) 
        {
            ClearTransformChildren(parent);
            parent.SetActive(false);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            if (!ReadOnly)
            {
                if (isLocal)
                    CVRTools.CleanAvatarGameObject(instance, TagsConverterAvatar(tags));
                else
                    CVRTools.CleanAvatarGameObjectNetwork(instance, friendsWith, TagsConverterAvatar(tags), forceShow, forceBlock);
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

        public GameObject GetSanitizedProp(GameObject parent, UgcTagsData tags, bool isOwnOrFriend, bool? visibility)
        {
            ClearTransformChildren(parent);
            parent.SetActive(false);
            var instance = GameObject.Instantiate(OriginalItem, parent.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            var convertedTags = TagsConverterProp(tags);
            bool forceBlock = visibility == false;
            bool forceShow = visibility == true;
            CVRTools.CleanPropGameObjectNetwork(instance, isOwnOrFriend, convertedTags, false, forceShow, forceBlock, false);
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
        public bool IsMatch(DownloadJob.ObjectType type, string id) => type == ObjectType && string.Equals(id, AssetId);       
        private static void ClearTransformChildren(GameObject playerAvatarParent)
        {
            if (playerAvatarParent.transform.childCount > 0)
                foreach (Transform tr in playerAvatarParent.transform)
                    UnityEngine.Object.Destroy(tr.gameObject);
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
        public static UgcTagsData TagsConverter(AssetManagement.PropTags tags) => new UgcTagsData
        {
            LoudAudio = tags.LoudAudio,
            LongRangeAudio = tags.LongRangeAudio,
            ContainsMusic = tags.ContainsMusic,
            ScreenEffects = tags.ScreenFx,
            FlashingColors = tags.FlashingColors,
            FlashingLights = tags.FlashingLights,
            ExtremelyBright = tags.ExtremelyBright,
            ParticleSystems = tags.ParticleSystems,
            Violence = tags.Violence,
            Gore = tags.Gore,
            Horror = tags.Horror,
            Jumpscare = tags.Jumpscare,
            ExtremelyHuge = tags.ExcessivelyHuge,
            ExtremelySmall = tags.ExcessivelySmall,
            Suggestive = tags.Suggestive,
            Nudity = tags.Nudity,
            AdminBanned = tags.AdminBanned,
            Incompatible = tags.Incompatible,
            LargeFileSize = tags.LargeFileSize,
            ExtremeFileSize = tags.ExtremeFileSize
        };
        private static AssetManagement.AvatarTags TagsConverterAvatar(UgcTagsData tags) => new AssetManagement.AvatarTags
        {
            LoudAudio = tags.LoudAudio,
            LongRangeAudio = tags.LongRangeAudio,
            ContainsMusic = tags.ContainsMusic,
            ScreenFx = tags.ScreenEffects,
            FlashingColors = tags.FlashingColors,
            FlashingLights = tags.FlashingLights,
            ExtremelyBright = tags.ExtremelyBright,
            ParticleSystems = tags.ParticleSystems,
            Violence = tags.Violence,
            Gore = tags.Gore,
            Horror = tags.Horror,
            Jumpscare = tags.Jumpscare,
            ExcessivelyHuge = tags.ExtremelyHuge,
            ExcessivelySmall = tags.ExtremelySmall,
            Suggestive = tags.Suggestive,
            Nudity = tags.Nudity,
            AdminBanned = tags.AdminBanned,
            Incompatible = tags.Incompatible,
            LargeFileSize = tags.LargeFileSize,
            ExtremeFileSize = tags.ExtremeFileSize
        };
        private static AssetManagement.PropTags TagsConverterProp(UgcTagsData tags) => new AssetManagement.PropTags
        {
            LoudAudio = tags.LoudAudio,
            LongRangeAudio = tags.LongRangeAudio,
            ContainsMusic = tags.ContainsMusic,
            ScreenFx = tags.ScreenEffects,
            FlashingColors = tags.FlashingColors,
            FlashingLights = tags.FlashingLights,
            ExtremelyBright = tags.ExtremelyBright,
            ParticleSystems = tags.ParticleSystems,
            Violence = tags.Violence,
            Gore = tags.Gore,
            Horror = tags.Horror,
            Jumpscare = tags.Jumpscare,
            ExcessivelyHuge = tags.ExtremelyHuge,
            ExcessivelySmall = tags.ExtremelySmall,
            Suggestive = tags.Suggestive,
            Nudity = tags.Nudity,
            AdminBanned = tags.AdminBanned,
            Incompatible = tags.Incompatible,
            LargeFileSize = tags.LargeFileSize,
            ExtremeFileSize = tags.ExtremeFileSize,
        };
        public static UgcTagsData TagsConverter(AssetManagement.AvatarTags tags) => new UgcTagsData
        {
            LoudAudio = tags.LoudAudio,
            LongRangeAudio = tags.LongRangeAudio,
            ContainsMusic = tags.ContainsMusic,
            ScreenEffects = tags.ScreenFx,
            FlashingColors = tags.FlashingColors,
            FlashingLights = tags.FlashingLights,
            ExtremelyBright = tags.ExtremelyBright,
            ParticleSystems = tags.ParticleSystems,
            Violence = tags.Violence,
            Gore = tags.Gore,
            Horror = tags.Horror,
            Jumpscare = tags.Jumpscare,
            ExtremelyHuge = tags.ExcessivelyHuge,
            ExtremelySmall = tags.ExcessivelySmall,
            Suggestive = tags.Suggestive,
            Nudity = tags.Nudity,
            AdminBanned = tags.AdminBanned,
            Incompatible = tags.Incompatible,
            LargeFileSize = tags.LargeFileSize,
            ExtremeFileSize = tags.ExtremeFileSize
        };
    }
}
