using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public class PoseHandling
    {
        public static bool debugFrame = false;

        public unsafe static Quaternion GetBoneRotation(ref BoneElement boneElement, ReadOnlySpan<float> muscles)
        {
            if (!boneElement.BoneExists)
                return Quaternion.identity;

            var id = boneElement.muscleIds;
            var dof = boneElement.dofExists;
            //  var rawMuscleValue = new Vector3(
            float rawX = dof.x ? muscles[id.x] : 0f;
            float rawY = dof.y ? muscles[id.y] : 0f;
            float rawZ = dof.z ? muscles[id.z] : 0f;
            //  var scale = new Vector3(
            float scaleX = rawX >= 0f ? boneElement.max.x : boneElement.min.x;
            float scaleY = rawY >= 0f ? boneElement.max.y : boneElement.min.y;
            float scaleZ = rawZ >= 0f ? boneElement.max.z : boneElement.min.z;

            float angleX = (scaleX * rawX) + boneElement.center.x;
            float angleY = (scaleY * rawY) + boneElement.center.y;
            float angleZ = (scaleZ * rawZ) + boneElement.center.z;
          //  var _angle = boneElement.center + (boneElement.sign * scale * rawMuscleValue);  // boneElement.center is pure guess based on docs.

            var twist = Quaternion.Euler(angleX, 0f, 0f);                                 // I couldn't find a way to add an offset with the humanoid rig import.
            var YZrot = Quaternion.Euler(0f, angleY, angleZ);   // Thanks to knah for figuring this out.
            //var rotY = Mathf.Tan(Deg2RadHalf * _angle.y);         // Mathf.Tan results the same values as HumanPoseHandler, math.tan or Math.Tan will differ slightly.
            //var rotZ = Mathf.Tan(Deg2RadHalf * _angle.z);         // it gets the same result as the Quaternion.Euler way, but that's easier to read or understand
            //var YZrot = new Quaternion(0f, rotY, rotZ, 1f);       // I don't know why this works
            YZrot.x = 0f;
            YZrot.Normalize();
            YZrot *= twist;

            var result = boneElement.preQ * YZrot * boneElement.postQInv;
            return result;
        }
        public unsafe static Quaternion GetBoneRotation(ref BoneElement boneElement, ref BoneElement parentBoneElement, ReadOnlySpan<float> muscles)
        {
            if (!boneElement.BoneExists)
                return Quaternion.identity;

            var id = boneElement.muscleIds;
            var dof = boneElement.dofExists;
            float3 raw = new float3(
                dof.x ? muscles[id.x] : 0f,
                dof.y ? muscles[id.y] : 0f,
                dof.z ? muscles[id.z] : 0f);
            float3 scale = new float3(
                raw.x >= 0f ? boneElement.max.x : boneElement.min.x,
                raw.y >= 0f ? boneElement.max.y : boneElement.min.y,
                raw.z >= 0f ? boneElement.max.z : boneElement.min.z);
            float3 angle = (scale * raw) + boneElement.center;

            var twist = Quaternion.Euler(angle.x * boneElement.twistValue, 0f, 0f);
            var YZrot = Quaternion.Euler(0f, angle.y, angle.z);
            YZrot.x = 0f;
            YZrot.Normalize();
            YZrot *= twist;

            var parentTwist = GetParentCarryoverTwist(ref boneElement, ref parentBoneElement, muscles);
            YZrot = parentTwist * YZrot;
            var result = boneElement.preQ * YZrot * boneElement.postQInv;
            
            return result;
        }

        private static unsafe Quaternion GetParentCarryoverTwist(ref BoneElement boneElement, ref BoneElement parentBoneElement, ReadOnlySpan<float> muscles)
        {
            // again knah comes for the rescure, and figured out how twists are calculated

            if (!parentBoneElement.dofExists.x)
                return Quaternion.identity;            

            var weight = 1f - parentBoneElement.twistValue;
            var value = muscles[parentBoneElement.muscleIds.x];
            var minmax = value > 0 ? parentBoneElement.max.x : parentBoneElement.min.x;
            var parentTwistAmount = weight * (value * minmax + parentBoneElement.center.x);
            if (parentTwistAmount == 0)
                return Quaternion.identity;
            var parentLocalTwistAxis = parentBoneElement.postQ * Vector3.right;
            var parentLocalTwist = Quaternion.AngleAxis(parentTwistAmount, parentLocalTwistAxis);
            return boneElement.preQInv * parentLocalTwist * boneElement.preQ;
        }

        public static Quaternion GetGenericRotationOfBone(BoneElement boneElement, Quaternion localRotation) 
        {
            if (!boneElement.BoneExists)
                return Quaternion.identity;
            return boneElement.preQInv * localRotation * boneElement.postQ;
        }

        public static Quaternion GetLocalRotationOfBone(BoneElement boneElement, Quaternion rotation)
        {
            if (!boneElement.BoneExists)
                return Quaternion.identity;
            return boneElement.preQ * rotation * boneElement.postQInv;
        }

        public static void CalibrateMuscles(Animator animator, BoneElement[] boneElements, IList<TransformInfoInit> transformInfos, bool transformsReadonly = false)
        {
            if (!animator)
                return;
            var avatar = animator.avatar;
            var hd = avatar.humanDescription;
            var human = hd.human;
            if (boneNames == null)
                boneNames = HumanTrait.BoneName.ToList();
            if (human != null && human.Length != 0)
            {
                for (int i = 0; i < human.Length; i++)
                {
                    var humanBone = human[i];
                    int index = boneNames.FindIndex(a => string.Equals(a, humanBone.humanName));
                    if (index < 0 || index == (int)HumanBodyBones.Hips)
                        continue;
                    AddBone(boneElements, transformInfos, avatar, humanBone.limit, index, true, transformsReadonly);
                }
            }
            else
            {
                // older versions of Unity didn't generate this data
                var defaultLimit = new HumanLimit { useDefaultValues = true };
                for (int i = 1; i < boneNames.Count; i++)
                {
                    // make sure the animator is active and enabled if using Unity 2021.2 or 2021.3
                    var exists = (bool)animator.GetBoneTransform((HumanBodyBones)i);
                    AddBone(boneElements, transformInfos, avatar, defaultLimit, i, exists, transformsReadonly);
                }
            }
            GetTwists(boneElements, hd);
        }

        private static void AddBone(BoneElement[] boneElements, IList<TransformInfoInit> transformInfos, Avatar avatar, HumanLimit humanLimit, int index, bool exists, bool transformsReadonly)
        {
            var boneData = boneElements[index];
            boneData.dofExists.w = exists;
            if (!exists)
                return;
            boneData.twistValue = 1f;
            boneData.humanBodyBoneId = (HumanBodyBones)index;
            boneData.preQ = GetPreRotation(avatar, (HumanBodyBones)index);
            boneData.postQ = GetPostRotation(avatar, (HumanBodyBones)index);
            boneData.preQInv = Quaternion.Inverse(boneData.preQ);
            boneData.postQInv = Quaternion.Inverse(boneData.postQ);
            int x = HumanTrait.MuscleFromBone(index, 0);
            int y = HumanTrait.MuscleFromBone(index, 1);
            int z = HumanTrait.MuscleFromBone(index, 2);
            boneData.dofExists.x = x >= 0;
            boneData.dofExists.y = y >= 0;
            boneData.dofExists.z = z >= 0;
            boneData.muscleIds.x = x;
            boneData.muscleIds.y = y;
            boneData.muscleIds.z = z;
            if (!humanLimit.useDefaultValues)
            {
                boneData.min = math.abs(humanLimit.min);
                boneData.max = humanLimit.max;
                boneData.center = humanLimit.center;
            }
            else
            {
                boneData.min.x = math.abs(HumanTrait.GetMuscleDefaultMin(x));
                boneData.min.y = math.abs(HumanTrait.GetMuscleDefaultMin(y));
                boneData.min.z = math.abs(HumanTrait.GetMuscleDefaultMin(z));
                boneData.max.x = HumanTrait.GetMuscleDefaultMax(x);
                boneData.max.y = HumanTrait.GetMuscleDefaultMax(y);
                boneData.max.z = HumanTrait.GetMuscleDefaultMax(z);
                boneData.center = float3.zero;
            }
            var sign = GetLimitSign(avatar, (HumanBodyBones)index);
            boneData.min *= sign; 
            boneData.max *= sign;
            transformInfos[index] = new TransformInfoInit
            {
                HasMultipleChildren = false,
                IsRoot = true,
                IsEnabled = index > 0,
                IsReadOnly = transformsReadonly,
                IsTransform = true
            };
            boneElements[index] = boneData;
        }
        public static unsafe void FixBoneTwist(Quaternion[] rotations, BoneElement[] boneElements, ReadOnlySpan<float> muscles)
        {
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.LeftUpperArm);
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.RightUpperArm);
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.LeftUpperLeg);
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.RightUpperLeg);
        }
        private static unsafe void FixLimbChain(Quaternion[] rotations, BoneElement[] boneElements, ReadOnlySpan<float> muscles, int startIndex)
        {
            var boneElementUpper = boneElements[startIndex];
            var boneElementMiddle = boneElements[startIndex + 2];
            var boneElementEnd = boneElements[startIndex + 4];
            rotations[startIndex]     = GetBoneRotation(ref boneElementUpper,  ref BoneElement.empty, muscles);
            rotations[startIndex + 2] = GetBoneRotation(ref boneElementMiddle, ref boneElementUpper,  muscles);
            rotations[startIndex + 4] = GetBoneRotation(ref boneElementEnd,    ref boneElementMiddle, muscles);
        }
        private static void GetTwists(BoneElement[] boneElements, HumanDescription hd)
        {
            boneElements[1].twistValue  = boneElements[2].twistValue  = 1f - hd.upperLegTwist;
            boneElements[3].twistValue  = boneElements[4].twistValue  = 1f - hd.lowerLegTwist;
            boneElements[13].twistValue = boneElements[14].twistValue = 1f - hd.upperArmTwist;
            boneElements[15].twistValue = boneElements[16].twistValue = 1f - hd.lowerArmTwist;
        }

        // private static readonly int[] UpperTwistBones = new int[] { 3, 4, 15, 16 };
        // private static readonly int[] LowerTwistBones = new int[] { 5, 6, 17, 18 };
        // private static readonly int[] UpperTwistMuscles = new int[] { 23, 31, 41, 50 };
        // private static readonly int[] LowerTwistMuscles = new int[] { 25, 33, 43, 52 };
        // internal static readonly HashSet<int> TwistingBones = new HashSet<int>() { 1, 2, 3, 4, 5, 6, 13, 14, 15, 16, 17, 18 };
        private static List<string> boneNames;
        private const BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly MethodInfo __GetPreRotation = typeof(Avatar).GetMethod("GetPreRotation", privateInstance);
        private static readonly MethodInfo __GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", privateInstance);
        private static readonly MethodInfo __GetLimitSign = typeof(Avatar).GetMethod("GetLimitSign", privateInstance);
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
