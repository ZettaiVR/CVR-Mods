using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ABI.CCK.Components;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core;
using static ABI_RC.Core.CVRTools;
using RootMotion.FinalIK;
using MagicaCloth;
using System.Runtime.CompilerServices;
namespace Zettai
{
    public static class Sanitizer
    {
        public static void CleanAvatarGameObject(GameObject avatar, AssetManagement.AvatarTags tags)
        {
            CleanAvatarGameObject(avatar, 8, true, tags, false, false, false, false);
            PlayerSetup.Instance.avatarTags = tags;
        }

        public static void CleanAvatarGameObjectNetwork(GameObject avatar, bool isFriend, AssetManagement.AvatarTags tags, bool forceShow, bool forceBlock)
        {
            CleanAvatarGameObject(avatar, 10, isFriend, tags, false, forceShow, forceBlock, false);
        }

        public struct Permissions
		{
			public static bool MatureContentAllowed => MetaPort.Instance.matureContentAllowed;
			private ulong Data;
            public ulong GetData => Data;
			public bool Visibility { get => GetBool(PermissionFlags.Visibility); private set => SetBool(PermissionFlags.Visibility, value); }
			public bool LoudAudio { get => GetBool(PermissionFlags.LoudAudio); set => SetBool(PermissionFlags.LoudAudio, value); }
			public bool LongRangeAudio { get => GetBool(PermissionFlags.LongRangeAudio); set => SetBool(PermissionFlags.LongRangeAudio, value); }
			public bool Music { get => GetBool(PermissionFlags.Music); set => SetBool(PermissionFlags.Music, value); }
			public bool ParticleSystems { get => GetBool(PermissionFlags.ParticleSystems); set => SetBool(PermissionFlags.ParticleSystems, value); }
			public bool Nudity { get => GetBool(PermissionFlags.Nudity); set => SetBool(PermissionFlags.Nudity, value); }
			public bool Suggestive { get => GetBool(PermissionFlags.Suggestive); set => SetBool(PermissionFlags.Suggestive, value); }
			public bool FlashingColors { get => GetBool(PermissionFlags.FlashingColors); set => SetBool(PermissionFlags.FlashingColors, value); }
			public bool FlashingLights { get => GetBool(PermissionFlags.FlashingLights); set => SetBool(PermissionFlags.FlashingLights, value); }
			public bool ExcessivelyHuge { get => GetBool(PermissionFlags.ExcessivelyHuge); private set => SetBool(PermissionFlags.ExcessivelyHuge, value); }
			public bool ExcessivelySmall { get => GetBool(PermissionFlags.ExcessivelySmall); private set => SetBool(PermissionFlags.ExcessivelySmall, value); }
			public bool ExtremelyBright { get => GetBool(PermissionFlags.ExtremelyBright); private set => SetBool(PermissionFlags.ExtremelyBright, value); }
			public bool ScreenEffects { get => GetBool(PermissionFlags.ScreenEffects); set => SetBool(PermissionFlags.ScreenEffects, value); }
			public bool Violence { get => GetBool(PermissionFlags.Violence); set => SetBool(PermissionFlags.Violence, value); }
			public bool Gore { get => GetBool(PermissionFlags.Gore); set => SetBool(PermissionFlags.Gore, value); }
			public bool Horror { get => GetBool(PermissionFlags.Horror); private set => SetBool(PermissionFlags.Horror, value); }
			public bool Jumpscare { get => GetBool(PermissionFlags.Jumpscare); private set => SetBool(PermissionFlags.Jumpscare, value); }
			public bool DynamicBone { get => GetBool(PermissionFlags.DynamicBone); private set => SetBool(PermissionFlags.DynamicBone, value); }
			public bool DynamicBoneCollision { get => GetBool(PermissionFlags.DynamicBoneCollision); private set => SetBool(PermissionFlags.DynamicBoneCollision, value); }
			public bool Cameras { get => GetBool(PermissionFlags.Cameras); private set => SetBool(PermissionFlags.Cameras, value); }
			public bool WindZones { get => GetBool(PermissionFlags.WindZones); private set => SetBool(PermissionFlags.WindZones, value); }
			public bool Lights { get => GetBool(PermissionFlags.Lights); private set => SetBool(PermissionFlags.Lights, value); }
			public bool PhysicsCollisionEnabled { get => GetBool(PermissionFlags.PhysicsCollisionEnabled); private set => SetBool(PermissionFlags.PhysicsCollisionEnabled, value); }
			public bool PhysicsCollidersEnabled { get => GetBool(PermissionFlags.PhysicsCollidersEnabled); private set => SetBool(PermissionFlags.PhysicsCollidersEnabled, value); }
			public bool AnimatorTriggersEnabled { get => GetBool(PermissionFlags.AnimatorTriggersEnabled); private set => SetBool(PermissionFlags.AnimatorTriggersEnabled, value); }
			public bool PhysicsCollidersSelfEnabled { get => GetBool(PermissionFlags.PhysicsCollidersSelfEnabled); private set => SetBool(PermissionFlags.PhysicsCollidersSelfEnabled, value); }
			public bool CustomShaders { get => GetBool(PermissionFlags.CustomShaders); private set => SetBool(PermissionFlags.CustomShaders, value); }
			public bool Collider { get => GetBool(PermissionFlags.Collider); private set => SetBool(PermissionFlags.Collider, value); }
			public bool MovementParentEnabled { get => GetBool(PermissionFlags.MovementParentEnabled); private set => SetBool(PermissionFlags.MovementParentEnabled, value); }
			private bool DisableAudio { get => GetBool(PermissionFlags.DisableAudio); set => SetBool(PermissionFlags.DisableAudio, value); }
			private bool AdminBanned { get => GetBool(PermissionFlags.AdminBanned); set => SetBool(PermissionFlags.AdminBanned, value); }
			private bool Incompatible { get => GetBool(PermissionFlags.Incompatible); set => SetBool(PermissionFlags.Incompatible, value); }
			private bool NudityTag { get => GetBool(PermissionFlags.NudityTag); set => SetBool(PermissionFlags.NudityTag, value); }
			private bool GoreTag { get => GetBool(PermissionFlags.GoreTag); set => SetBool(PermissionFlags.GoreTag, value); }
			public bool Audio => LoudAudio || LongRangeAudio || Music || DisableAudio;
			public bool Hide => Nudity || Suggestive || FlashingColors || FlashingLights || ExtremelyBright || ScreenEffects 
				|| Violence || Gore || Horror || Jumpscare || ExcessivelyHuge || ExcessivelySmall || AdminBanned || Incompatible 
				|| (!MatureContentAllowed && (NudityTag || GoreTag)) || Visibility;

			public Permissions(AssetManagement.AvatarTags tags, bool disableAudio, bool isFriend, bool forceShow)
			{
				Data = 0;
				AdminBanned = tags.AdminBanned;
				Incompatible = tags.Incompatible;
				NudityTag = tags.Nudity;
				GoreTag = tags.Gore;
				DisableAudio = disableAudio;
				settings = MetaPort.Instance.settings._settings;
				Visibility = CheckContentFilter("ContentFilterVisibility", isFriend, isApplicable: true);
				LoudAudio = CheckContentFilter("ContentFilterLoudAudio", isFriend, tags.LoudAudio);
				LongRangeAudio = CheckContentFilter("ContentFilterLongRangeAudio", isFriend, tags.LongRangeAudio);
				Music = CheckContentFilter("ContentFilterMusic", isFriend, tags.ContainsMusic);
				ParticleSystems = CheckContentFilter("ContentFilterParticleSystems", isFriend, isApplicable: true);
				Nudity = CheckContentFilter("ContentFilterNudity", isFriend, tags.Nudity);
				Suggestive = CheckContentFilter("ContentFilterSuggestive", isFriend, tags.Suggestive);
				FlashingColors = CheckContentFilter("ContentFilterFlashingColors", isFriend, tags.FlashingColors);
				FlashingLights = CheckContentFilter("ContentFilterFlashingLights", isFriend, tags.FlashingLights);
				ExtremelyBright = CheckContentFilter("ContentFilterExtremelyBright", isFriend, tags.ExtremelyBright);
				ScreenEffects = CheckContentFilter("ContentFilterScreenEffects", isFriend, tags.ScreenFx);
				Violence = CheckContentFilter("ContentFilterViolence", isFriend, tags.Violence);
				Gore = CheckContentFilter("ContentFilterGore", isFriend, tags.Gore);
				Horror = CheckContentFilter("ContentFilterHorror", isFriend, tags.Horror);
				Jumpscare = CheckContentFilter("ContentFilterJumpscare", isFriend, tags.Jumpscare);
				DynamicBone = CheckContentFilter("ContentFilterDynamicBone", isFriend, isApplicable: true) && !forceShow;
				DynamicBoneCollision = CheckContentFilter("ContentFilterDynamicBoneCollision", isFriend, isApplicable: true) && !forceShow;
				Cameras = CheckContentFilter("ContentFilterCameras", isFriend, isApplicable: true) && !forceShow;
				WindZones = CheckContentFilter("ContentFilterWindZones", isFriend, isApplicable: true) && !forceShow;
				Lights = CheckContentFilter("ContentFilterLights", isFriend, isApplicable: true) && !forceShow;
				PhysicsCollisionEnabled = CheckContentFilter("InteractionPhysicsCollisionEnabled", isFriend, isApplicable: true);
				PhysicsCollidersEnabled = CheckContentFilter("InteractionPhysicsCollidersEnabled", isFriend, isApplicable: true);
				AnimatorTriggersEnabled = CheckContentFilter("InteractionAnimatorTriggersEnabled", isFriend, isApplicable: true);
				PhysicsCollidersSelfEnabled = !CheckContentFilter("InteractionPhysicsCollidersSelfEnabled", isFriend, isApplicable: true);
				CustomShaders = CheckContentFilter("ContentFilterCustomShaders", isFriend, isApplicable: true);
				Collider = CheckContentFilter("ContentFilterCollider", isFriend, isApplicable: true);
				MovementParentEnabled = CheckContentFilter("InteractionMovementParentEnabled", isFriend, isApplicable: true);
				ExcessivelyHuge = CheckContentFilter("ContentFilterExcessivelyHuge", isFriend, tags.ExcessivelyHuge);
				ExcessivelySmall = CheckContentFilter("ContentFilterExcessivelySmall", isFriend, tags.ExcessivelySmall);
			}
            public bool GetValue(int tag) => GetValue((CVRAvatarAdvancedTaggingEntry.Tags)(1 << tag));
            public void SetValue(int tag, bool value) => SetValue((CVRAvatarAdvancedTaggingEntry.Tags)(1 << tag), value);

            private bool GetValue(CVRAvatarAdvancedTaggingEntry.Tags tag) 
            {
                switch (tag)
                {
                    case CVRAvatarAdvancedTaggingEntry.Tags.LoudAudio:
                        return LoudAudio;
                    case CVRAvatarAdvancedTaggingEntry.Tags.LongRangeAudio:
                        return LongRangeAudio;
                    case CVRAvatarAdvancedTaggingEntry.Tags.ScreenFx:
                        return ScreenEffects;
                    case CVRAvatarAdvancedTaggingEntry.Tags.FlashingColors:
                        return FlashingColors;
                    case CVRAvatarAdvancedTaggingEntry.Tags.FlashingLights:
                        return FlashingLights;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Violence:
                        return Violence;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Gore:
                        return Gore;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Suggestive:
                        return Suggestive;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Nudity:
                        return Nudity;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Horror:
                        return Horror;
                    default:
                        return false;
                }
            }
            private void SetValue(CVRAvatarAdvancedTaggingEntry.Tags tag, bool value)
            {
                switch (tag)
                {
                    case CVRAvatarAdvancedTaggingEntry.Tags.LoudAudio:
                        LoudAudio = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.LongRangeAudio:
                         LongRangeAudio = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.ScreenFx:
                         ScreenEffects = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.FlashingColors:
                         FlashingColors = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.FlashingLights:
                         FlashingLights = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Violence:
                         Violence = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Gore:
                         Gore = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Suggestive:
                         Suggestive = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Nudity:
                         Nudity = value;
                        break;
                    case CVRAvatarAdvancedTaggingEntry.Tags.Horror:
                        Horror = value;
                        break;
                    default:
                        return;
                }
            }

            private static Dictionary<string, CVRSettingsValue> settings;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static bool CheckContentFilter(string name, bool isFriend, bool isApplicable)
			{
				var value = !settings.TryGetValue(name, out CVRSettingsValue cvrSettingsValue)
					? 0
					: cvrSettingsValue.GetValueInt();
				switch (value)
				{
					case 0:
						return isApplicable;
					case 1:
						return !isFriend && isApplicable;
					default:
						return false;
				}
			}
            private enum PermissionFlags
			{
				Visibility,
				LoudAudio,
				LongRangeAudio,
				Music,
				ParticleSystems,
				Nudity,
				Suggestive,
				FlashingColors,
				FlashingLights,
				ExcessivelyHuge,
				ExcessivelySmall,
				ExtremelyBright,
				ScreenEffects,
				Violence,
				Gore,
				Horror,
				Jumpscare,
				DynamicBone,
				DynamicBoneCollision,
				Cameras,
				WindZones,
				Lights,
				PhysicsCollisionEnabled,
				PhysicsCollidersEnabled,
				AnimatorTriggersEnabled,
				PhysicsCollidersSelfEnabled,
				CustomShaders,
				Collider,
				MovementParentEnabled,
				DisableAudio,
				AdminBanned,
				Incompatible,
				NudityTag,
				GoreTag,
			}
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private bool GetBool(PermissionFlags index) => (Data >> (int)index & 0x1) == 1;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void SetBool(PermissionFlags index, bool boolToSet)
			{
				ulong shifted = ((ulong)1) << (int)index;
				Data = Data & ~shifted | (boolToSet ? shifted : 0);
			}
		}

        public static void CleanAvatarGameObject(GameObject avatar, int layer, bool isFriend, AssetManagement.AvatarTags tags, bool disableAudio = false, 
            bool forceShow = false, bool forceBlock = false, bool secondRun = false)
        {
            PlayerDescriptor playerDescriptor = avatar.GetComponentInParent<PlayerDescriptor>();
            CVRAvatar cvrAvatar = avatar.GetComponent<CVRAvatar>();
            Permissions p = new Permissions(tags, disableAudio, isFriend, forceShow);

            ApplyAdvancedTags(cvrAvatar, ref p);
            bool hide = p.Hide;
            if (forceShow)
                hide = false;
            if (forceBlock)
                hide = true;
            if (!MetaPort.Instance.matureContentAllowed && (tags.Nudity || tags.Gore))
                hide = true;

            SanitizeGameObject(avatar, hide ? AssetType.HiddenAvatar : AssetType.Avatar);
            bool playerlocal = playerDescriptor != null && !string.IsNullOrEmpty(playerDescriptor.ownerId) && playerDescriptor.ownerId[0] == '_';
            ApplyBlockedAvatar(avatar, out CVRBlockedAvatarController cvrBlockedAvatarController);
            Animator animator = avatar.GetComponent<Animator>();
            Transform hips = null;
            if (animator != null && animator.avatar && animator.avatar.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            }
            ProcessComponents(layer, cvrAvatar, p, hide, p.Audio, playerlocal, hips);
            if (p.CustomShaders)
            {
                var renderers = from comp in components where comp is Renderer select (comp as Renderer);
                foreach (var renderer in renderers)
                    ReplaceShaders(renderer);
            }
            bool ExperimentalAdvancedSafetyFilterFriends = MetaPort.Instance.settings.GetSettingsBool("ExperimentalAdvancedSafetyFilterFriends");
            if (!forceShow && ((isFriend && ExperimentalAdvancedSafetyFilterFriends) || !isFriend))
            {
                CleanGameObject(avatar.gameObject);
            }
            if (hide)
                HideAvatar(avatar, cvrBlockedAvatarController, animator, cvrAvatar);
            SetGameObjectLayerFromList(layer);
            if (layer == 10)
            {
                PlaceHapticsTriggersAndPointers(avatar);
            }
            GenerateDefaultPointer(avatar, layer);
            AvatarCleaned.Invoke(avatar);
        }
        private static void SetGameObjectLayerFromList(int layer) 
        {
            foreach (var item in transforms)
                item.gameObject.layer = layer;
            transforms.Clear();
        }
        private static void SetGameObjectLayerRecursive(GameObject avatar, int layer)
        {
            transforms.Clear();
            rectTransforms.Clear();
            avatar.GetComponentsInChildren(true, transforms);
            avatar.GetComponentsInChildren(true, rectTransforms);
            foreach (var item in transforms)
            {
                item.gameObject.layer = layer;
            }
            foreach (var item in rectTransforms)
            {
                item.gameObject.layer = layer;
            }
            transforms.Clear();
            rectTransforms.Clear();
        }
        private static void ProcessComponents(int layer, CVRAvatar cvrAvatar, Permissions p, bool hide, bool removeAudio, bool local, Transform hips)
        {
            ulong particleCount = 0;
            int audioSourceCount = 0;
            transforms.Clear();
            foreach (Component component in components)
            {
                if (component == null)
                    continue;
                if (component is Transform tr)
                {
                    transforms.Add(tr);
                    continue;
                }
                if (component is RectTransform rtr)
                {
                    transforms.Add(rtr);
                    continue;
                }
                if (component is Animator animator)
                {
                    SanitizeAnimationEvents(animator);
                }
                if (component is VRIK vrik)
                {
                    SanitizeUnityEvents(vrik.solver.locomotion.onLeftFootstep);
                    SanitizeUnityEvents(vrik.solver.locomotion.onRightFootstep);
                }
                if (hide)
                    continue;
                Type type = component.GetType();
                if (CheckTypes(type, component, layer, p, removeAudio, ref audioSourceCount))
                    continue;
                if (CheckLists(type, component, layer, cvrAvatar, p, local, hips, ref particleCount))
                    continue;
            }
        }

        private static void SetLayer(int layer, Component component) => component.gameObject.layer = layer;

        private static bool CheckTypes(Type type, Component component, int layer, Permissions p, bool removeAudio, ref int audioSourceCount) 
        {
            switch (component)
            {
                case Camera camera:
                    if (p.Cameras || camera.targetTexture == null)
                        RemoveComponent(component, type);
                    return true;
                case CVRBlitter _:
                case Projector _:
                case CVRTexturePropertyParser _:
                    if (p.Cameras)
                        RemoveComponent(component, type);
                    return true;
                case AudioSource source:
                    if (removeAudio || audioSourceCount >= 100)
                    {
                        if (audioSourceCount >= 100)

                        RemoveComponent(component, type);
                        return true;
                    }
                    source.outputAudioMixerGroup = RootLogic.Instance.avatarSfx;
                    source.gameObject.tag = "ABI_AVATAR_AUDIO";
                    source.spatialize = source.spatialBlend >= 1f;
                    audioSourceCount++;
                    return true;
                case CVRMovementParent _:
                    if (p.MovementParentEnabled)
                        RemoveComponent(component, type);
                    return true;
                case WindZone zone:
                    if (p.WindZones)
                    {
                        RemoveComponent(component, type);
                        return true;
                    }
                    CVRWindZoneManager.Instance.AddWindZone(zone);
                    return true;
                case CVRAdvancedAvatarSettingsPointer _:
                case CVRToggleStatePointer _:
                case CVRPointer _:
                    {
                        if (layer != 8)
                        {
                            if (p.AnimatorTriggersEnabled)
                                RemoveComponent(component, type);
                            return true;
                        }
                        CVRPointer cVRPointer = (CVRPointer)component;
                        cVRPointer.isLocalPointer = true;
                        break;
                    }
            }
            return false;
        }
        private static bool CheckLists(Type type, Component component, int layer, CVRAvatar cvrAvatar, Permissions p, bool local, Transform hips, ref ulong particleCount) 
        {
            if (dynamicBoneColliderComponents.Contains(type))
            {
                if (p.DynamicBoneCollision)
                    RemoveComponent(component, type);
                return true;
            }
            if (localComponents.Contains(type))
            {
                if (layer != 8)
                    RemoveComponent(component, type);
                return true;
            }
            if (lightComponents.Contains(type))
            {
                if (p.Lights)
                    RemoveComponent(component, type);
                return true; 
            }
            if (colliderComponents.Contains(type))
            {
                if (p.Collider)
                {
                    RemoveComponent(component, type);
                    return true;
                }
                Collider collider = (Collider)component;
                if (collider.isTrigger)
                {
                    cvrAvatar.avatarColliders.Add(new CVRAvatarCollider
                    {
                        collider = collider,
                        isTrigger = collider.isTrigger
                    });
                    return true;
                }
                if (!p.PhysicsCollidersEnabled && (layer == 10 || ((hips == null || !collider.transform.IsChildOf(hips)) && p.PhysicsCollidersSelfEnabled)))
                {
                    if (collider.gameObject.GetComponent<Rigidbody>() == null)
                    {
                        Rigidbody rigidbody = collider.gameObject.AddComponent<Rigidbody>();
                        rigidbody.useGravity = false;
                        rigidbody.isKinematic = true;
                    }
                    cvrAvatar.avatarColliders.Add(new CVRAvatarCollider
                    {
                        collider = collider,
                        isTrigger = collider.isTrigger
                    });
                    return true;
                }
            }
            if (particleComponents.Contains(type))
            {
                if (p.ParticleSystems)
                {
                    RemoveComponent(component, type);
                    return true;
                }
                if (component is ParticleSystem particleSystem)
                {
                    if (p.PhysicsCollisionEnabled)
                    {
                        ParticleSystem.CollisionModule collision = particleSystem.collision;
                        collision.collidesWith &= -257;
                        collision.collidesWith &= -5;
                    }
                    else
                    {
                        ParticleSystem.CollisionModule collision2 = particleSystem.collision;
                        collision2.colliderForce = Mathf.Min(collision2.colliderForce, 1000000f);
                        collision2.collidesWith &= -5;
                    }
                    particleCount += (uint)particleSystem.main.maxParticles;
                }
                return true;
            }
            if (rootComponents.Contains(type))
            {
                return true;
            }
            if (rendererComponets.Contains(type))
            {
                if (local && Hologram)
                {
                    try
                    {
                        Renderer renderer = (Renderer)component;
                        Material[] materials = renderer.materials;
                        for (int j = 0; j < materials.Length; j++)
                        {
                            materials[j] = MetaPort.Instance.hologrammMaterial;
                        }
                        renderer.materials = materials;
                    }
                    catch (Exception)
                    {
                        if (MemoryCache.enableLog.Value)
                            MelonLoader.MelonLogger.Msg("Renderer Material could not be changed.");
                    }
                }
            }
            if (dynamicBoneComponents.Contains(type))
            {
                if (p.DynamicBone)
                {
                    RemoveComponent(component, type);
                    return true;
                }
                if (component is DynamicBone || layer != 8 && layer != 9)
                    return true;
                switch (component)
                {
                    case MagicaBoneCloth boneCloth:
                        boneCloth.activeDuringSetup = boneCloth.enabled;
                        boneCloth.enabled = false;
                        break;
                    case MagicaMeshCloth meshCloth:
                        meshCloth.activeDuringSetup = meshCloth.enabled;
                        meshCloth.enabled = false;
                        break;
                    case MagicaBoneSpring boneSpring:
                        boneSpring.activeDuringSetup = boneSpring.enabled;
                        boneSpring.enabled = false;
                        break;
                    case MagicaMeshSpring clothSpring:
                        clothSpring.activeDuringSetup = clothSpring.enabled;
                        clothSpring.enabled = false;
                        break;
                }
                return true;
            }
            return false;
        }
        private static void HideAvatar(GameObject avatar, CVRBlockedAvatarController cvrBlockedAvatarController, Animator animator, CVRAvatar cvrAvatar)
        {
            if (cvrBlockedAvatarController != null)
            {
                cvrBlockedAvatarController.SetActive();
                return;
            }
            float viewHeight = cvrAvatar != null ? cvrAvatar.viewPosition.y : 1f;
            GameObject torso, head = null, leftHand = null, rightHand = null;
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (!animator.isHuman)
                {
                    torso = UnityEngine.Object.Instantiate(MetaPort.Instance.blockedAvatarTorso, avatar.transform);
                    torso.transform.localScale = GetScale(viewHeight, torso.transform.lossyScale);
                }
                else
                {
                    torso = UnityEngine.Object.Instantiate(MetaPort.Instance.blockedAvatarTorso, animator.GetBoneTransform(HumanBodyBones.Spine));
                    head = UnityEngine.Object.Instantiate(MetaPort.Instance.blockedAvatarHead, animator.GetBoneTransform(HumanBodyBones.Head));
                    leftHand = UnityEngine.Object.Instantiate(MetaPort.Instance.blockedAvatarHandLeft, animator.GetBoneTransform(HumanBodyBones.LeftHand));
                    rightHand = UnityEngine.Object.Instantiate(MetaPort.Instance.blockedAvatarHandRight, animator.GetBoneTransform(HumanBodyBones.RightHand));
                    torso.transform.localScale = GetScale(viewHeight, torso.transform.lossyScale);
                    var scale = head.transform.lossyScale;
                    if (scale.y != 0f)
                        head.transform.localScale = GetScale(viewHeight, scale);
                    leftHand.transform.localScale = GetScale(viewHeight, leftHand.transform.lossyScale);
                    rightHand.transform.localScale = GetScale(viewHeight, rightHand.transform.lossyScale);
                }
                var controller = avatar.AddComponent<CVRBlockedAvatarController>();
                controller.avatar = cvrAvatar;
                controller.animator = animator;
                controller.head = head;
                controller.torso = torso;
                controller.handLeft = leftHand;
                controller.handRight = rightHand;
                controller.Initialize();
            }
        }
        private static Vector3 GetScale(float viewHeight, Vector3 scale) => new Vector3(viewHeight / scale.x, viewHeight / scale.y, viewHeight / scale.z);

        private static void ApplyBlockedAvatar(GameObject avatar, out CVRBlockedAvatarController cvrBlockedAvatarController) => cvrBlockedAvatarController = (CVRBlockedAvatarController)avatar.GetComponent(typeof(CVRBlockedAvatarController));

        private static void TagHandledByAdvancedTagging(CVRAvatar avatar, int i) => avatar.ApplyFilterByTag((CVRAvatarAdvancedTaggingEntry.Tags)(1 << i));
        private static void ApplyAdvancedTags(CVRAvatar cvrAvatar, ref Permissions p)
        {
            for (int i = 0; i < 9; i++)
                if (p.GetValue(i))
                {
                    TagHandledByAdvancedTagging(cvrAvatar, i);
                    p.SetValue(i, false);
                }
        }

        public enum AssetType
        {
            Avatar = 1,
            Scene = 2,
            Prop = 4,
            HiddenAvatar = 8,
            Other = 16,
            Unknown = 128
        }
        private class ComponentDependency
        {
            public bool hasDependency;
            public Type[] types = new Type[3];
            private static readonly ComponentDependency[] m_emptyArray = new ComponentDependency[0];
            public static ComponentDependency[] EmptyArray { get { return m_emptyArray; } }
        }
        /// <summary>
        /// Removes all components from gameObject and its children that are not on a whitelist.  
        /// </summary>
        /// <param name="gameObject">The GameObject to sanitize.</param>
        /// <param name="assetType">The type of asset to sanitize that determines the allowed components.</param>
        /// <param name="removeFromAllowedList">Component types to remove from the default allowed components lists for the asset type.</param>
        /// <returns>The time taken in milliseconds.</returns>
        public static uint SanitizeGameObject(GameObject gameObject, AssetType assetType, List<Type> removeFromAllowedList = null)
        {
            stopWatch.Stop();
            stopWatch.Reset();
            stopWatch.Start();

            var allowedTypes = GetAllowedTypesHashSet(assetType);
            if (removeFromAllowedList != null)
            {
                foreach (var item in removeFromAllowedList)
                {
                    allowedTypes.Remove(item);
                }
            }

            removedComponents.Clear();
            gameObject.GetComponentsInChildren(true, components);
            for (int i = 0; i < components.Count; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;
                var renderer = component as Renderer;
                if (renderer)
                {
                    LimitRenderer(renderer);
                }
                Type compType = component.GetType();
                if (compType == transformType || allowedTypes.Contains(compType))
                {
                    if (greyList.Contains(compType))
                    {
                        var mono = component as MonoBehaviour;
                        if (mono)
                            mono.enabled = false;
                    }
                    continue;
                }
                RemoveComponent(component, compType);
            }
            if (removedComponents.Length > 0)
            {
                if (MemoryCache.enableLog.Value)
                    MelonLoader.MelonLogger.Msg(removedComponents.ToString(), gameObject);
            }
            gameObject.GetComponentsInChildren(true, components);
            stopWatch.Stop();
            return (uint)(stopWatch.Elapsed.TotalMilliseconds * 1000);
        }
        private static void LimitRenderer(Renderer renderer)
        {
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = Math.Min(1, renderer.sortingOrder);
        }
        private static void RemoveComponent(Component component, Type type, List<Component> componentsOnGameObject = null)
        {
            var gameObject = component.gameObject;
            if (componentsOnGameObject == null)
            {
                componentsOnGameObject = componentsOnGo;
                gameObject.GetComponents(componentsOnGameObject);
            }
            for (int i = 0; i < componentsOnGameObject.Count; i++)
            {
                Component thisComp = componentsOnGameObject[i];
                if (!thisComp || thisComp == component)
                    continue;
                CheckComponent(type, componentsOnGameObject, thisComp);
            }
            if (MemoryCache.enableLog.Value)
                AppendToRemoveLog(gameObject, type);
            UnityEngine.Object.DestroyImmediate(component, true);
        }

        private static void CheckComponent(Type type, List<Component> componentsOnGameObject, Component thisComp)
        {
            Type thisType = thisComp.GetType();
            var dep = GetComponentDependency(thisType);
            for (int j = 0; j < dep.Length; j++)
            {
                if (!dep[j].hasDependency)
                    continue;
                CheckDependency(type, componentsOnGameObject, thisComp, thisType, dep, j);
            }
        }

        private static void CheckDependency(Type type, List<Component> componentsOnGameObject, Component thisComp, Type thisType, ComponentDependency[] dep, int j)
        {
            for (int k = 0; k < 3; k++)
                if (dep[j].types[k] == type && thisComp)
                    RemoveComponent(thisComp, thisType, componentsOnGameObject);
        }

        private static void AppendToRemoveLog(GameObject gameObject, Type type)
        {
            removedComponents.Append("Removed component '");
            removedComponents.Append(type.Name);
            removedComponents.Append("' from gameObject '");
            removedComponents.Append(gameObject.name);
            removedComponents.AppendLine("'.");
        }
        private static ComponentDependency[] GetComponentDependency(Type type)
        {
            if (componentDependencies.TryGetValue(type, out ComponentDependency[] deps))
            {
                return deps;
            }
            var attr = type.GetCustomAttributes(typeof(RequireComponent), true);
            bool hasDependency = false;
            if (attr == null || attr.Length == 0)
            {
                deps = Array.Empty<ComponentDependency>();
                componentDependencies[type] = deps;
                return deps;
            }
            tempComponentDependency.Clear();
            for (int i = 0; i < attr.Length; i++)
            {
                if (!(attr[i] is RequireComponent requireComponent))
                    continue;
                var dep = new ComponentDependency();
                if (requireComponent.m_Type0 != transformType)
                    dep.types[0] = requireComponent.m_Type0;
                if (requireComponent.m_Type1 != transformType)
                    dep.types[1] = requireComponent.m_Type1;
                if (requireComponent.m_Type2 != transformType)
                    dep.types[2] = requireComponent.m_Type2;
                if (dep.types[0] != null || dep.types[1] != null || dep.types[2] != null)
                {
                    dep.hasDependency = true;
                    hasDependency = true;
                    tempComponentDependency.Add(dep);
                }
            }
            deps = hasDependency ? tempComponentDependency.ToArray() : Array.Empty<ComponentDependency>();
            componentDependencies[type] = deps;
            tempComponentDependency.Clear();
            return deps;
        }
        private static List<ComponentDependency> tempComponentDependency = new List<ComponentDependency>();
        private static HashSet<Type> GetAllowedTypesHashSet(AssetType assetType)
        {
            if (allowedTypesDict.TryGetValue(assetType, out HashSet<Type> set))
            {
                return set;
            }
            set = new HashSet<Type>();
            allowedTypesDict.Add(assetType, set);
            switch (assetType)
            {
                case AssetType.Avatar:
                    {
                        AddListToSet(componentWhiteList, set);
                        break;
                    }
                case AssetType.HiddenAvatar:
                    {
                        AddListToSet(rootComponents, set);
                        return set;
                    }
                case AssetType.Scene:
                    {
                        AddListToSet(forgottenTypesWorld, set);
                        AddListToSet(componentWhiteList, set);
                        AddListToSet(propWhitelist, set);
                        break;
                    }
                case AssetType.Prop:
                    {
                        AddListToSet(forgottenTypesProps, set);
                        AddListToSet(propWhitelist, set);
                        break;
                    }
                case AssetType.Other:
                case AssetType.Unknown:
                default:
                    break;
            }
            AddListToSet(forgottenTypesAvatar, set);
            AddListToSet(rootComponents, set);
            AddListToSet(particleComponents, set);
            AddListToSet(dynamicBoneComponents, set);
            AddListToSet(dynamicBoneColliderComponents, set);
            AddListToSet(localComponents, set);
            AddListToSet(colliderComponents, set);
            AddListToSet(lightComponents, set);
            AddListToSet(rendererComponets, set);
            return set;
        }
        private static void AddListToSet(IEnumerable<Type> whitelist, HashSet<Type> set)
        {
            foreach (Type name in whitelist)
            {
                var types = TypesFromType(name);
                foreach (Type type in types)
                    set.Add(type);
            }
        }
        private static IEnumerable<Type> TypesFromType(Type type)
        {
            string name = type.Name;
            if (typeNameCache.TryGetValue(name, out var types))
                return types;
            types = TypeArrayForType(type);
            typeNameCache.Add(name, types);
            return types;
        }
        private static IEnumerable<Type> TypeArrayForType(Type type)
        {
            if (type == null)
                return Array.Empty<Type>();
            GetAssemblies();
            matchingTypes.Clear();
            try
            {
                matchingTypes.Add(type);
                for (int i = 0; i < allAssemblies.Length; i++)
                {
                    if (!typeAssemblyCache.TryGetValue(allAssemblies[i], out var types))
                    {
                        types = allAssemblies[i].GetTypes();
                        typeAssemblyCache[allAssemblies[i]] = types;
                    }
                    foreach (Type v in types)
                        if (v != type && type.IsAssignableFrom(v))
                            matchingTypes.Add(v);
                }
                return matchingTypes.ToList();
            }
            finally
            {
                matchingTypes.Clear();
            }
        }

        private static void GetAssemblies()
        {
            if (allAssemblies == null)
                allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        private static readonly IReadOnlyList<Type> forgottenTypesAvatar = new Type[]
        {
            typeof(MagicaPhysicsManager),
            typeof(CVRTexturePropertyParser),
            typeof(AudioSource),
            typeof(CVRMovementParent),
            typeof(CVRParameterStream),
            typeof(CVRAdvancedAvatarSettingsTrigger),
            typeof(CVRAdvancedAvatarSettingsPointer),
            typeof(Animation),
        }; 
        private static readonly IReadOnlyList<Type> forgottenTypesWorld = new Type[]
        {
            typeof(CVRBlitter),
            typeof(CVRMaterialDriver),
            typeof(CVRVariableBuffer),
        };
        private static readonly IReadOnlyList<Type> forgottenTypesProps = new Type[]
        {
            typeof(CVRMaterialDriver),
        };
        private static bool Hologram => MemoryCache.enableHologram.Value;
        private static System.Reflection.Assembly[] allAssemblies = null;
        private static readonly System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        private static readonly Type transformType = typeof(Transform);
        private static readonly StringBuilder removedComponents = new StringBuilder(10 * 1024);
        private static readonly List<Transform> transforms = new List<Transform>(1000);
        private static readonly List<RectTransform> rectTransforms = new List<RectTransform>(1000);
        private static readonly List<Component> components = new List<Component>(1000);
        private static readonly List<Component> componentsOnGo = new List<Component>(10);
        private static readonly List<Material> materials = new List<Material>(1000);
        private static readonly HashSet<Type> matchingTypes = new HashSet<Type>();
        private static readonly HashSet<Type> greyList = new HashSet<Type>();
        private static readonly Dictionary<string, IEnumerable<Type>> typeNameCache = new Dictionary<string, IEnumerable<Type>>();
        private static readonly Dictionary<System.Reflection.Assembly, IEnumerable<Type>> typeAssemblyCache = new Dictionary<System.Reflection.Assembly, IEnumerable<Type>>();
        private static readonly Dictionary<Type, ComponentDependency[]> componentDependencies = new Dictionary<Type, ComponentDependency[]>();
        private static readonly Dictionary<AssetType, HashSet<Type>> allowedTypesDict = new Dictionary<AssetType, HashSet<Type>>();
    }
}
