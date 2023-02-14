using ABI_RC.Core.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Zettai
{
    class Setup
    {
        private static List<string> boneNames;
        private static readonly List<Thread> threads = new List<Thread>();
        public static void AddPlayer(PuppetMaster player)
        {
            var data = new NetIkData();
            data.puppetMaster = player;
            var animator = data.animator = player?._animator;
            if (!animator || !animator.isHuman || !animator.avatar)
                return;
            Update.puppetMasters[animator] = player;
            data.avatar = animator.avatar;
            for (int i = 0; i < data.rotTransforms.Length; i++)
            {
                data.rotTransforms[i] = animator.GetBoneTransform((HumanBodyBones)i);
            }
            data.hips = data.rotTransforms[(int)HumanBodyBones.Hips];
            data.root = animator.transform;
            CalibrateMuscles(data);
            Update.players.Add(player, data);
            Update.allPlayers.Add(player);
        }
        internal static void Init(int count = 2)
        {
            count = Mathf.Clamp(count, 1, 8);
            StartNewThreads("NetIK", Update.NetIkProcess, count);
            Update.threadCount = count;
        }
        
        public static bool GetPlayer(PuppetMaster player, ref NetIkData value) => Update.players.TryGetValue(player, out value);
        public static bool GetPlayer(Animator animator, ref NetIkData value)
        {
            if (Update.puppetMasters.TryGetValue(animator, out var pm)) { 
                return Update.players.TryGetValue(pm, out value);}
            return false;
        }

        public static void StartNewThreads(string name, ThreadStart threadStart, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var thread = new Thread(threadStart)
                {
                    IsBackground = true,
                    Name = count == 1 ? $"[NetIK] {name}" : $"[NetIK] {name} {i + 1}"
                };
                threads.Add(thread);
                thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                thread.Start();
            }
        }
        private static void CalibrateMuscles(NetIkData netIkData)
        {
            if (!netIkData.animator)
                return;
            var animator = netIkData.animator;
            var avatar = animator.avatar;
            var hd = avatar.humanDescription;
            var human = hd.human;
            for (int i = 0; i < human.Length; i++)
            {
                var humanBone = human[i];
                if (boneNames == null)
                    boneNames = HumanTrait.BoneName.ToList();
                int index = boneNames.FindIndex(a => string.Equals(a, humanBone.humanName));
                if (index < 0 || index == (int)HumanBodyBones.Hips)
                    continue;
                var boneData = netIkData.boneElements[index];
                boneData.dofExists.w = true;
                boneData.humanBodyBoneId = (HumanBodyBones)index;
                boneData.sign = GetLimitSign(avatar, (HumanBodyBones)index);
                boneData.preQ = GetPreRotation(avatar, (HumanBodyBones)index);
                boneData.postQ = GetPostRotation(avatar, (HumanBodyBones)index);
                boneData.preQInv = Quaternion.Inverse(boneData.preQ);
                boneData.postQInv = Quaternion.Inverse(boneData.postQ);
                boneData.transform = animator.GetBoneTransform((HumanBodyBones)index);
                var limit = humanBone.limit;
                int x = HumanTrait.MuscleFromBone(index, 0);
                int y = HumanTrait.MuscleFromBone(index, 1);
                int z = HumanTrait.MuscleFromBone(index, 2);
                boneData.dofExists.x = x >= 0;
                boneData.dofExists.y = y >= 0;
                boneData.dofExists.z = z >= 0;
                boneData.muscleIds.x = x;
                boneData.muscleIds.y = y;
                boneData.muscleIds.z = z;
                if (!limit.useDefaultValues)
                {
                    boneData.max = limit.max;
                    boneData.min = limit.min;
                    //boneData.center = limit.center;
                    boneData.minAbs.x = Mathf.Abs(boneData.min.x);
                    boneData.minAbs.y = Mathf.Abs(boneData.min.y);
                    boneData.minAbs.z = Mathf.Abs(boneData.min.z);
                }
                else
                {
                    boneData.max.x = HumanTrait.GetMuscleDefaultMax(x);
                    boneData.max.y = HumanTrait.GetMuscleDefaultMax(y);
                    boneData.max.z = HumanTrait.GetMuscleDefaultMax(z);
                    //boneData.center = Vector3.zero;
                    boneData.min.x = HumanTrait.GetMuscleDefaultMin(x);
                    boneData.min.y = HumanTrait.GetMuscleDefaultMin(y);
                    boneData.min.z = HumanTrait.GetMuscleDefaultMin(z);
                    boneData.minAbs.x = Mathf.Abs(boneData.min.x);
                    boneData.minAbs.y = Mathf.Abs(boneData.min.y);
                    boneData.minAbs.z = Mathf.Abs(boneData.min.z);
                }
                netIkData.transformInfos[index] = new TransformInfoInit
                {
                    HasMultipleChildren = false,
                    IsRoot = true,
                    IsEnabled = true,
                    IsReadOnly = false,
                    IsTransform = true
                };
                netIkData.boneElements[index] = boneData;
            }

            foreach (int i in twistyBones)
            {
                var boneData = netIkData.boneElements[i];
                switch (boneData.humanBodyBoneId)
                {
                    case HumanBodyBones.LeftUpperArm:
                    case HumanBodyBones.RightUpperArm:
                        boneData.twistValue = 1f - hd.upperArmTwist;
                        break;
                    case HumanBodyBones.LeftLowerArm:
                        boneData.twistValue = 1f - hd.lowerArmTwist;
                        boneData.mixIndex = (int)MuscleNamesEnum.LeftArmTwistInOut;
                        break;
                    case HumanBodyBones.RightLowerArm:
                        boneData.twistValue = 1f - hd.lowerArmTwist;
                        boneData.mixIndex = (int)MuscleNamesEnum.RightArmTwistInOut;
                        break;
                    case HumanBodyBones.LeftLowerLeg:
                        boneData.twistValue = 1f - hd.lowerLegTwist;
                        boneData.mixIndex = (int)MuscleNamesEnum.LeftUpperLegTwistInOut;
                        break;
                    case HumanBodyBones.RightLowerLeg:
                        boneData.twistValue = 1f - hd.lowerLegTwist;
                        boneData.mixIndex = (int)MuscleNamesEnum.RightUpperLegTwistInOut;
                        break;
                    case HumanBodyBones.LeftUpperLeg:
                    case HumanBodyBones.RightUpperLeg:
                        boneData.twistValue = 1f - hd.upperLegTwist;
                        break;
                    case HumanBodyBones.LeftHand:
                        boneData.mixIndex = (int)MuscleNamesEnum.LeftForearmTwistInOut;
                        break;
                    case HumanBodyBones.RightHand:
                        boneData.mixIndex = (int)MuscleNamesEnum.RightForearmTwistInOut;
                        break;
                    case HumanBodyBones.LeftFoot:
                        boneData.mixIndex = (int)MuscleNamesEnum.LeftLowerLegTwistInOut;
                        break;
                    case HumanBodyBones.RightFoot:
                        boneData.mixIndex = (int)MuscleNamesEnum.RightLowerLegTwistInOut;
                        break;
                    default:
                        break;
                }
                netIkData.boneElements[i] = boneData;
            }
        }
        internal static readonly int[] twistyBones = new int[]
          {
            (int)HumanBodyBones.LeftUpperLeg,
            (int)HumanBodyBones.RightUpperLeg,
            (int)HumanBodyBones.LeftLowerLeg,
            (int)HumanBodyBones.RightLowerLeg,
            (int)HumanBodyBones.LeftFoot,
            (int)HumanBodyBones.RightFoot,
            (int)HumanBodyBones.LeftUpperArm,
            (int)HumanBodyBones.RightUpperArm,
            (int)HumanBodyBones.LeftLowerArm,
            (int)HumanBodyBones.RightLowerArm,
            (int)HumanBodyBones.LeftHand,
            (int)HumanBodyBones.RightHand,
          };
       
        private static readonly MethodInfo __GetPreRotation = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo __GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo __GetLimitSign = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
        private static Vector3 GetLimitSign(Avatar _avatar, HumanBodyBones humanBodyBone) => GetLimitSignDelegate(_avatar, humanBodyBone);
        private static Quaternion GetPreRotation(Avatar _avatar, HumanBodyBones humanBodyBone) => GetPreRotationDelegate(_avatar, humanBodyBone);
        private static Quaternion GetPostRotation(Avatar _avatar, HumanBodyBones humanBodyBone) => GetPostRotationDelegate(_avatar, humanBodyBone);

        private static readonly Func<Avatar, HumanBodyBones, Quaternion> GetPreRotationDelegate = 
            (Func<Avatar, HumanBodyBones, Quaternion>)Delegate.CreateDelegate(typeof(Func<Avatar, HumanBodyBones, Quaternion>), __GetPreRotation);

        private static readonly Func<Avatar, HumanBodyBones, Quaternion> GetPostRotationDelegate = 
            (Func<Avatar, HumanBodyBones, Quaternion>)Delegate.CreateDelegate(typeof(Func<Avatar, HumanBodyBones, Quaternion>), __GetPostRotation);

        private static readonly Func<Avatar, HumanBodyBones, Vector3> GetLimitSignDelegate = 
            (Func<Avatar, HumanBodyBones, Vector3>)Delegate.CreateDelegate(typeof(Func<Avatar, HumanBodyBones, Vector3>), __GetLimitSign);
    }
}
