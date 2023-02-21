﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public class PoseHandling
    {
        public static Quaternion GetBoneRotation(BoneElement boneElement, float[] muscles)
        {
            var dof = boneElement.dofExists;
            if (!dof.w)
                return Quaternion.identity;
            var id = boneElement.muscleIds;
            var rawMuscleValue = new float3(
                dof.x ? muscles[id.x] : 0f, 
                dof.y ? muscles[id.y] : 0f,
                dof.z ? muscles[id.z] : 0f);
            var scale = new float3(
                rawMuscleValue.x >= 0f ? boneElement.max.x : boneElement.min.x,
                rawMuscleValue.y >= 0f ? boneElement.max.y : boneElement.min.y,
                rawMuscleValue.z >= 0f ? boneElement.max.z : boneElement.min.z);
            var _angle = boneElement.center + (boneElement.sign * scale * rawMuscleValue);  // boneElement.center is pure guess based on docs.
            var twist = Quaternion.Euler(_angle.x, 0f, 0f);                                 // I couldn't find a way to add an offset with the humanoid rig import.
            var rotY = Mathf.Tan(Deg2RadHalf * _angle.y);       // Mathf.Tan results the same values as HumanPoseHandler, math.tan or Math.Tan will differ slightly.
            var rotZ = Mathf.Tan(Deg2RadHalf * _angle.z);       // Don't worry, you can use Mathf in Burst jobs just fine.
            var tPoseQ = new Quaternion(0f, rotY, rotZ, 1f);    // Thanks to knah for figuring this out.
            tPoseQ.Normalize();
            tPoseQ *= twist;
            var result = boneElement.preQ * tPoseQ * boneElement.postQInv;
            return result;
        }
        public static void CalibrateMuscles(Animator animator, BoneElement[] boneElements, IList<TransformInfoInit> transformInfos)
        {
            if (!animator)
                return;
            var avatar = animator.avatar;
            var hd = avatar.humanDescription;
            var human = hd.human;

            if (human.Length != 0)
            {
                for (int i = 0; i < human.Length; i++)
                {
                    var humanBone = human[i];
                    int index = boneNames.FindIndex(a => string.Equals(a, humanBone.humanName));
                    if (index < 0 || index == (int)HumanBodyBones.Hips)
                        continue;
                    AddBone(boneElements, transformInfos, avatar, humanBone.limit, index, true);
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
                    AddBone(boneElements, transformInfos, avatar, defaultLimit, i, exists);
                }
            }
            var z = math.abs(boneElements[3].preQ.eulerAngles.z);
            boneElements[3].middleMultiplier = z < 90f || z > 270f ? -1f : 1f;  // dirty hack
            z = math.abs(boneElements[4].preQ.eulerAngles.z);
            boneElements[4].middleMultiplier = z < 90f || z > 270f ? -1f : 1f;


            GetTwists(boneElements, hd);
        }
        private static void AddBone(BoneElement[] boneElements, IList<TransformInfoInit> transformInfos, Avatar avatar, HumanLimit humanLimit, int index, bool exists)
        {
            var boneData = boneElements[index];
            boneData.dofExists.w = exists;
            if (!exists)
                return;
            boneData.middleMultiplier = 1f;
            boneData.humanBodyBoneId = (HumanBodyBones)index;
            boneData.sign = GetLimitSign(avatar, (HumanBodyBones)index);
            boneData.preQ = GetPreRotation(avatar, (HumanBodyBones)index);
            boneData.postQ = GetPostRotation(avatar, (HumanBodyBones)index);
            boneData.preQInv = Quaternion.Inverse(boneData.preQ);
            boneData.postQInv = Quaternion.Inverse(boneData.postQ);
            //      boneData.transform = animator.GetBoneTransform((HumanBodyBones)index);
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
            transformInfos[index] = new TransformInfoInit
            {
                HasMultipleChildren = false,
                IsRoot = true,
                IsEnabled = true,
                IsReadOnly = false,
                IsTransform = true
            };
            boneElements[index] = boneData;
        }
        public static void FixBoneTwist(Quaternion[] rotations, BoneElement[] boneElements, float[] muscles)
        {
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.LeftUpperArm, (int)MuscleNamesEnum.LeftArmTwistInOut);
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.RightUpperArm, (int)MuscleNamesEnum.RightArmTwistInOut);
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.LeftUpperLeg, (int)MuscleNamesEnum.LeftUpperLegTwistInOut);
            FixLimbChain(rotations, boneElements, muscles, (int)HumanBodyBones.RightUpperLeg, (int)MuscleNamesEnum.RightUpperLegTwistInOut);
        }
        private static void FixLimbChain(Quaternion[] rotations, BoneElement[] boneElements, float[] muscles, int startIndex, int startMuscle) 
        {
            var boneElementUpper = boneElements[startIndex];
            var upperMuscle = muscles[startMuscle];
            var boneElementMiddle = boneElements[startIndex + 2];
            var middleMuscle = muscles[startMuscle + 2];
            var boneElementEnd = boneElements[startIndex + 4];
            FixLimbs(rotations, upperMuscle, middleMuscle, boneElementUpper, boneElementMiddle, boneElementEnd, startIndex);
        }

        static void FixLimbs(Quaternion[] rotations, float upperMuscle, float middleMuscle, BoneElement boneElementUpper,
             BoneElement boneElementMiddle, BoneElement boneElementEnd, int startIndex)
        {
            var originalRotUpper = boneElementUpper.preQInv * rotations[startIndex] * boneElementUpper.postQ;
            var originalRotMiddle = boneElementMiddle.preQInv * rotations[startIndex + 2] * boneElementMiddle.postQ;
            var originalRotEnd = boneElementEnd.preQInv * rotations[startIndex + 4] * boneElementEnd.postQ;

            upperMuscle *= boneElementUpper.twistValue;
            var scale = upperMuscle >= 0 ? boneElementUpper.max.x : boneElementUpper.min.x;
            var angleUpper = scale * upperMuscle * boneElementUpper.sign.x;
            var twistUpper = Quaternion.Euler(-angleUpper, 0f, 0f);
            var newRotUpper = boneElementUpper.preQ * originalRotUpper * twistUpper * boneElementUpper.postQInv;

            middleMuscle *= boneElementMiddle.twistValue;
            var scaleMiddle = middleMuscle >= 0 ? boneElementMiddle.max.x : boneElementMiddle.min.x;
            var angleMiddle = boneElementMiddle.sign.x * scaleMiddle * middleMuscle;
            var twistMiddle = Quaternion.Euler(-angleMiddle, 0f, 0f);
            var newRotMiddle = Quaternion.Euler(0f, angleUpper * boneElementMiddle.middleMultiplier, 0f) * boneElementMiddle.preQ * originalRotMiddle * twistMiddle * boneElementMiddle.postQInv;

            var newRotEnd = Quaternion.Euler(0f, angleMiddle, 0f) * boneElementEnd.preQ * originalRotEnd * boneElementEnd.postQInv;

            rotations[startIndex] = newRotUpper;
            rotations[startIndex + 2] = newRotMiddle;
            rotations[startIndex + 4] = newRotEnd;
        }

        private static void GetTwists(BoneElement[] boneElements, HumanDescription hd)
        {
            boneElements[1].twistValue  = boneElements[2].twistValue  = 1f - hd.upperLegTwist;
            boneElements[3].twistValue  = boneElements[4].twistValue  = 1f - hd.lowerLegTwist;
            boneElements[13].twistValue = boneElements[14].twistValue = 1f - hd.upperArmTwist;
            boneElements[15].twistValue = boneElements[16].twistValue = 1f - hd.lowerArmTwist;
        }

        private static List<string> boneNames;
        private const float Deg2RadHalf = Mathf.Deg2Rad * 0.5f;
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
