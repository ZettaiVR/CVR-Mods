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
        public static Quaternion GetBoneRotation(BoneElement boneElement, float[] muscles)
        {
            var dof = boneElement.dofExists;
            if (!dof.w)
                return Quaternion.identity;
            var id = boneElement.muscleIds;
            var rawMuscleValue = new float3(
                dof.x ? muscles[id.x] * boneElement.twistValue : 0f, 
                dof.y ? muscles[id.y] : 0f,
                dof.z ? muscles[id.z] : 0f);
            var scale = new float3(
                dof.x ? (rawMuscleValue.x >= 0f ? boneElement.max.x : boneElement.minAbs.x) : 0f,
                dof.y ? (rawMuscleValue.y >= 0f ? boneElement.max.y : boneElement.minAbs.y) : 0f,
                dof.z ? (rawMuscleValue.z >= 0f ? boneElement.max.z : boneElement.minAbs.z) : 0f);
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
            for (int i = 0; i < human.Length; i++)
            {
                var humanBone = human[i];
                if (boneNames == null)
                    boneNames = HumanTrait.BoneName.ToList();
                int index = boneNames.FindIndex(a => string.Equals(a, humanBone.humanName));
                if (index < 0 || index == (int)HumanBodyBones.Hips)
                    continue;
                var boneData = boneElements[index];
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
                    boneData.center = limit.center;
                }
                else
                {
                    boneData.max.x = HumanTrait.GetMuscleDefaultMax(x);
                    boneData.max.y = HumanTrait.GetMuscleDefaultMax(y);
                    boneData.max.z = HumanTrait.GetMuscleDefaultMax(z);
                    boneData.center = float3.zero;
                    boneData.min.x = HumanTrait.GetMuscleDefaultMin(x);
                    boneData.min.y = HumanTrait.GetMuscleDefaultMin(y);
                    boneData.min.z = HumanTrait.GetMuscleDefaultMin(z);
                }
                boneData.minAbs = math.abs(boneData.min);
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
            GetTwists(boneElements, hd);
        }

        public static void FixBoneTwist(quaternion[] rotations, BoneElement[] boneElements, float[] muscles)
        {
            var startIndex = (int)HumanBodyBones.LeftUpperArm;
            var startMuscle = 41;
            var boneElementUpperLeft = boneElements[startIndex];
            var upperMuscleLeft = muscles[startMuscle];
            var boneElementForearmLeft = boneElements[startIndex + 2];
            var forearmMuscleLeft = muscles[startMuscle + 2];

            var boneElementUpperRight = boneElements[startIndex + 1];
            var upperMuscleRight = muscles[startMuscle + 9];
            var boneElementForearmRight = boneElements[startIndex + 3];
            var forearmMuscleRight = muscles[startMuscle + 11];

            FixLimbs(rotations, upperMuscleLeft, forearmMuscleLeft, boneElementUpperLeft, boneElementForearmLeft, startIndex);
            FixLimbs(rotations, upperMuscleRight, forearmMuscleRight, boneElementUpperRight, boneElementForearmRight, startIndex + 1);

            startIndex = (int)HumanBodyBones.LeftUpperLeg;
            startMuscle = 23;
            boneElementUpperLeft = boneElements[startIndex];
            upperMuscleLeft = muscles[startMuscle];
            boneElementForearmLeft = boneElements[startIndex + 2];
            forearmMuscleLeft = muscles[startMuscle + 2];

            boneElementUpperRight = boneElements[startIndex + 1];
            upperMuscleRight = muscles[startMuscle + 8];
            boneElementForearmRight = boneElements[startIndex + 3];
            forearmMuscleRight = muscles[startMuscle + 10];

            FixLimbs(rotations, upperMuscleLeft, forearmMuscleLeft, boneElementUpperLeft, boneElementForearmLeft, startIndex);
            FixLimbs(rotations, upperMuscleRight, forearmMuscleRight, boneElementUpperRight, boneElementForearmRight, startIndex + 1);
        }
        private static void FixLimbs(quaternion[] rotations, float upperMuscle, float middleMuscle, BoneElement boneElementUpper,
            BoneElement boneElementMiddle, int startIndex)
        {
            upperMuscle *= 1f - boneElementUpper.twistValue;
            var scale = upperMuscle >= 0 ? boneElementUpper.max.x : boneElementUpper.minAbs.x;
            var angleUpper = boneElementUpper.sign.x * scale * upperMuscle;
            var twistUpper = Quaternion.Euler(angleUpper, 0f, 0f);
            var rot = boneElementUpper.preQInv * rotations[startIndex] * boneElementUpper.postQ;
            rot = boneElementUpper.preQ * rot * Quaternion.Inverse(twistUpper) * boneElementUpper.postQInv;
            rotations[startIndex] = rot;

            middleMuscle *= 1f - boneElementMiddle.twistValue;
            var scaleMiddle = middleMuscle >= 0 ? boneElementMiddle.max.x : boneElementMiddle.minAbs.x;
            var angleMiddle = boneElementMiddle.sign.x * scaleMiddle * middleMuscle;
            var twistMiddle = Quaternion.Euler(angleMiddle, 0f, 0f);
            rot = boneElementMiddle.preQInv * rotations[startIndex + 2] * boneElementMiddle.postQ;
            rot = boneElementMiddle.preQ * rot * Quaternion.Inverse(twistMiddle) * boneElementMiddle.postQInv;
            var rotMiddleEuler = rot.eulerAngles;
            rotMiddleEuler.y += angleUpper;
            var rotMiddle = Quaternion.Euler(rotMiddleEuler);
            rotations[startIndex + 2] = rotMiddle;

            var rotEndEuler = ((Quaternion)rotations[startIndex + 4]).eulerAngles;
            rotEndEuler.y += angleMiddle;
            var rotFoot = Quaternion.Euler(rotEndEuler);
            rotations[startIndex + 4] = rotFoot;

            //would make more sense but no.
            //var rotHand = boneElementHand.preQ * Quaternion.Euler(angleMiddle, 0f, 0f) * boneElementHand.tPoseQ * boneElementHand.postQInv;
            //rotations[startIndex + 4] = rotHand;
        }
      
        private static void GetTwists(BoneElement[] boneElements, HumanDescription hd)
        {
            foreach (int i in twistyBones)
            {
                var boneData = boneElements[i];
                switch (boneData.humanBodyBoneId)
                {
                    case HumanBodyBones.LeftUpperArm:
                    case HumanBodyBones.RightUpperArm:
                        boneData.twistValue = 1f - hd.upperArmTwist;
                        break;
                    case HumanBodyBones.LeftLowerArm:
                    case HumanBodyBones.RightLowerArm:
                        boneData.twistValue = 1f - hd.lowerArmTwist;
                        break;
                    case HumanBodyBones.LeftLowerLeg:
                    case HumanBodyBones.RightLowerLeg:
                        boneData.twistValue = 1f - hd.lowerLegTwist;
                        break;
                    case HumanBodyBones.LeftUpperLeg:
                    case HumanBodyBones.RightUpperLeg:
                        boneData.twistValue = 1f - hd.upperLegTwist;
                        break;
                    default:
                        break;
                }
                boneElements[i] = boneData;
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
