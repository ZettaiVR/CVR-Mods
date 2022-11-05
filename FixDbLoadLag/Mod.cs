using ABI_RC.Core.Player;
using HarmonyLib;
using MelonLoader;
using System.Collections.Generic;
using UnityEngine;
using Zettai;

[assembly: MelonInfo(typeof(FixDbLoadLag), "FixDbLoadLag", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]

namespace Zettai
{
    public class FixDbLoadLag : MelonMod
	{
		private static MelonPreferences_Entry<bool> enableDbLagPatch;
        public override void OnApplicationStart()
		{
			var category = MelonPreferences.CreateCategory("Zettai");
			enableDbLagPatch = category.CreateEntry("enableDbLagPatch", true, "Enable Dynamic bone loading lag patch");
		}
        private static bool FriendsWith(string owner)
        {
            if (string.IsNullOrEmpty(owner))
                return false;
            foreach (var friend in ABI_RC.Core.InteractionSystem.ViewManager.Instance.FriendList)
                if (string.Equals(owner, friend.UserId))
                    return true;
            return false;
        }
        [HarmonyPatch(typeof(CVRDynamicBoneManager), nameof(CVRDynamicBoneManager.UpdateComponents))]
        class UpdateDbComponentsPatch
        {
            public static bool letItRun = false;
            static bool update = false;
            static bool Prefix()
            {
                if (!enableDbLagPatch.Value)
                    return true;
                if (!letItRun)
                {
                    update = true;
                    return false;
                }
                if (update)
                    CVRDynamicBoneManagerUpdateComponents.Prefix();
                update = false;
                return false;
            }
        }
        [HarmonyPatch(typeof(DbJobsColliderUpdate), nameof(DbJobsColliderUpdate.Update))]
        class UpdateDbComponentsRun
        {
            static bool Prefix()
            {
                if (!enableDbLagPatch.Value)
                    return true;
                UpdateDbComponentsPatch.letItRun = true;
                CVRDynamicBoneManager.UpdateComponents();
                UpdateDbComponentsPatch.letItRun = false;
                return true;
            }
        }
        [HarmonyPatch(typeof(DbJobsAvatarManager), nameof(DbJobsAvatarManager.Awake))]
        class DbJobsAvatarManagerAwake
        {
            readonly static List<DynamicBoneCollider> colliders = new List<DynamicBoneCollider>();
            readonly static List<DynamicBoneCollider> collidersTemp2 = new List<DynamicBoneCollider>();
            static void Postfix(DbJobsAvatarManager __instance)
            {
                if (!enableDbLagPatch.Value)
                    return;
                var gameObject = __instance.gameObject;
                var playerDescriptor = gameObject.GetComponent<PlayerDescriptor>();
                var playerId = playerDescriptor == null ? "" : playerDescriptor.ownerId;
                var playerSetup = __instance.GetComponentInParent<PlayerSetup>();
                var isSelf = playerSetup != null;
                gameObject.GetComponentsInChildren(true, colliders);
                var collidersToData = new List<DynamicBoneCollider>();
                foreach (var coll in colliders)
                {
                    if (coll.m_Bound == DynamicBoneColliderBase.Bound.Outside && coll.enableInterCollision
                        && GetColliderMaxSize(coll) <= CVRDynamicBoneManager._maxColliderSize)
                        collidersToData.Add(coll);
                }
                var collidersOnHands = new List<DynamicBoneCollider>();
                var collidersOnHandsFingers = new List<DynamicBoneCollider>();

                GetHandColliders(gameObject, collidersOnHands, collidersOnHandsFingers);

                var data = new AvatarDbData
                {
                    avatarManager = __instance,
                    isFriend = FriendsWith(playerId),
                    isSelf = isSelf,
                    collidersOnAll = collidersToData,
                    collidersOnHandsAndFingers = collidersOnHandsFingers,
                    collidersOnHands = collidersOnHands
                };
                AvatarDbData.avatars[__instance] = data;
                colliders.Clear();
                AvatarDbData.avatarColliders[__instance] = new HashSet<DbJobsAvatarManager>();
            }
            private static float GetColliderMaxSize(DynamicBoneCollider c)
            {
                var rad1 = c.m_Radius;
                var rad2 = c.m_Radius2;
                var height = c.m_Height;
                var scale = c.gameObject.transform.lossyScale.x;
                float scaledRadius = rad1 * scale;
                float scaledRadius2 = rad2 * scale;
                float scaledHeightHalf = height * scale * 0.5f;
                if (scaledRadius2 <= 0 || Mathf.Abs(scaledRadius - scaledRadius2) < 0.01f)
                {
                    float h = height * 0.5f - rad1;
                    if (h <= 0)
                        return Mathf.Max(scaledRadius, scaledRadius2);
                    else
                        return Mathf.Max(scaledRadius, scaledHeightHalf * 0.5f);
                }
                else
                {
                    float halfHeight = height * 0.5f;
                    float r = Mathf.Max(rad1, rad2);
                    if (halfHeight - r <= 0)
                        return Mathf.Max(scaledRadius, scaledRadius2);
                    else
                    {
                        float h0 = halfHeight - rad1;
                        float h1 = halfHeight - rad2;
                        float max = Mathf.Max(h1, h0);
                        return Mathf.Max(max, Mathf.Max(scaledRadius, scaledHeightHalf * 0.5f));
                    }
                }
            }
            private static void GetHandColliders(GameObject gameObject, List<DynamicBoneCollider> collidersOnHands, List<DynamicBoneCollider> collidersOnHandsFingers)
            {
                var animator = gameObject.GetComponent<Animator>();
                if (!animator)
                    return;
                var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

                if (!leftHand || !rightHand)
                    return;
                var r1 = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                var l1 = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
                var r2 = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
                var l2 = animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal);
                var r3 = animator.GetBoneTransform(HumanBodyBones.RightRingProximal);
                var l3 = animator.GetBoneTransform(HumanBodyBones.LeftRingProximal);
                var r4 = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                var l4 = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                var r5 = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                var l5 = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);


                colliders.Clear();
                leftHand.GetComponentsInChildren(true, colliders);
                collidersOnHandsFingers.AddRange(colliders);
                colliders.Clear();
                rightHand.GetComponentsInChildren(true, colliders);
                collidersOnHandsFingers.AddRange(colliders);
                collidersOnHandsFingers.RemoveAll(a => a.m_Bound == DynamicBoneColliderBase.Bound.Inside || GetColliderMaxSize(a) > CVRDynamicBoneManager._maxColliderSize);
                collidersOnHands.AddRange(collidersOnHandsFingers);
                colliders.Clear();
                r1?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                l1?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                r2?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                l2?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                r3?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                l3?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                r4?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                l4?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                r5?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
                l5?.GetComponentsInChildren(true, colliders);
                collidersOnHands.RemoveAll(a => colliders.Contains(a));
                colliders.Clear();
            }
        }
        [HarmonyPatch(typeof(DbJobsAvatarManager), nameof(DbJobsAvatarManager.OnDestroy))]
        class DbJobsAvatarManagerOnDestroy
        {
            static void Postfix(DbJobsAvatarManager __instance)
            {
                if (!enableDbLagPatch.Value)
                    return;
                AvatarDbData.avatars.Remove(__instance);
                AvatarDbData.avatarColliders.Remove(__instance);
            }
        }
        //[HarmonyPatch(typeof(CVRDynamicBoneManager), nameof(CVRDynamicBoneManager.UpdateComponents))]
        class CVRDynamicBoneManagerUpdateComponents
        {
            public static readonly Dictionary<DbJobsAvatarManager, HashSet<DynamicBoneCollider>> addAvatarColliders = new Dictionary<DbJobsAvatarManager, HashSet<DynamicBoneCollider>>();
            public static readonly Dictionary<DbJobsAvatarManager, HashSet<DynamicBoneCollider>> removeAvatarColliders = new Dictionary<DbJobsAvatarManager, HashSet<DynamicBoneCollider>>();
            internal static bool Prefix()
            {
                if (!enableDbLagPatch.Value)
                    return true;
                foreach (var avatarEntry in AvatarDbData.avatars)
                {
                    var avatar = avatarEntry.Key;
                    if (!addAvatarColliders.TryGetValue(avatar, out var set))
                        addAvatarColliders[avatar] = set = new HashSet<DynamicBoneCollider>();
                    if (!removeAvatarColliders.TryGetValue(avatar, out var removeSet))
                        removeAvatarColliders[avatar] = removeSet = new HashSet<DynamicBoneCollider>();
                    removeSet.Clear();
                    removeSet.UnionWith(set);
                    set.Clear();
                    if (CVRDynamicBoneManager._colliderRelationFromMe != CVRDynamicBoneManager.ColliderRelation.None) 
                    {
                        foreach (var insideAvatarEntry in AvatarDbData.avatars)
                        {
                            if (avatar == insideAvatarEntry.Key ||
                                CVRDynamicBoneManager._colliderRelationFromMe == CVRDynamicBoneManager.ColliderRelation.Friends && !insideAvatarEntry.Value.isFriend)
                                continue;
                            // can add
                            switch (CVRDynamicBoneManager._colliderPlacement)
                            {
                                case CVRDynamicBoneManager.ColliderPlacement.Hands:
                                    set.UnionWith(insideAvatarEntry.Value.collidersOnHands);
                                    break;
                                case CVRDynamicBoneManager.ColliderPlacement.HandAndFingers:
                                    set.UnionWith(insideAvatarEntry.Value.collidersOnHandsAndFingers);
                                    break;
                                case CVRDynamicBoneManager.ColliderPlacement.Everything:
                                    set.UnionWith(insideAvatarEntry.Value.collidersOnAll);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    // anything not added but present in previous set will remain in remove set
                    foreach (var coll in set)
                        removeSet.Remove(coll);
                }
                foreach (var item in removeAvatarColliders)
                {
                    foreach (var collider in item.Value)
                        item.Key.RemoveCollider(collider);
                    item.Value.Clear();
                }
                foreach (var item in addAvatarColliders)
                    foreach (var collider in item.Value)
                        item.Key.AddCollider(collider);

                return false;
            }
        }
    }
    public class AvatarDbData
    {
        public static readonly Dictionary<DbJobsAvatarManager, AvatarDbData> avatars = new Dictionary<DbJobsAvatarManager, AvatarDbData>();
        public static readonly Dictionary<DbJobsAvatarManager, HashSet<DbJobsAvatarManager>> avatarColliders = new Dictionary<DbJobsAvatarManager, HashSet<DbJobsAvatarManager>>();
        public DbJobsAvatarManager avatarManager;
        public List<DynamicBoneCollider> collidersOnAll;
        public List<DynamicBoneCollider> collidersOnHandsAndFingers;
        public List<DynamicBoneCollider> collidersOnHands;
        public bool isSelf;
        public bool isFriend;
    }
}
