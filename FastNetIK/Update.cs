using ABI_RC.Core.Player;
using MelonLoader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Zettai
{
    class Update
    {
        public static bool writeStarted = false;
        public static JobHandle writeJobHandle;
        private static bool rebuildTransformAccess = true;

        private static float time = 0f;
        internal static volatile bool AbortThreads = false;
        internal static int threadCount = 2;
        private static int playerCount = 0;
        internal static readonly SemaphoreSlim startProcessing = new SemaphoreSlim(0, 20);
        private static readonly SemaphoreSlim doneProcessing = new SemaphoreSlim(0, 20);
        private static readonly float[] staticMuscles = new float[95];
        private static readonly int BoneCount = (int)HumanBodyBones.LastBone;
        internal static readonly List<TransformInfoInit> transformInfoInitList = new List<TransformInfoInit>();
        internal static readonly List<Transform> transformsAccessList = new List<Transform>();
        private static readonly HashSet<PuppetMaster> removePlayers = new HashSet<PuppetMaster>();
        internal static readonly HashSet<PuppetMaster> allPlayers = new HashSet<PuppetMaster>();
        internal static readonly Dictionary<PuppetMaster, NetIkData> players = new Dictionary<PuppetMaster, NetIkData>();
        internal static readonly Dictionary<Animator, PuppetMaster> puppetMasters = new Dictionary<Animator, PuppetMaster>();
        private static readonly ConcurrentQueue<PuppetMaster> playersToProcess = new ConcurrentQueue<PuppetMaster>();
        internal static int playersProcessed = 0;
        internal static NativeArray<TransformInfoInit> transformInfoInitArray;
        internal static TransformAccessArray transformsAccess;
        internal static void NetIkProcess()
        {
            float[] muscles = new float[95];
            while (!AbortThreads)
            {
                startProcessing.Wait(20);
                try
                {
                    while (!playersToProcess.IsEmpty)
                    {
                        if (playersToProcess.TryDequeue(out var player))
                        {
                            if (players.TryGetValue(player, out var data))
                            {
                                UpdatePlayer(data, time, muscles);
                            }
                            Interlocked.Increment(ref playersProcessed);
                        }
                    }
                    if (playersProcessed == playerCount && doneProcessing.CurrentCount == 0)
                    {
                        doneProcessing.Release();
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"NetIkProcess failed! '{e.Message}' ");
                }
            }
        }
        internal static void AbortAllThreads()
        {
            AbortThreads = true;
        }
        public static void StartProcessing()
        {
            if (doneProcessing.CurrentCount > 0)
                for (int i = 0; i < doneProcessing.CurrentCount; i++)
                    doneProcessing.Wait(0);

            writeStarted = false;
            RemovePlayers();
            time = Time.time;
            playerCount = allPlayers.Count;
            foreach (var player in allPlayers)
            {
                playersToProcess.Enqueue(player);
            }
            if (startProcessing.CurrentCount < 20)
                startProcessing.Release(Mathf.Clamp(threadCount - startProcessing.CurrentCount, 1, 20));
        }
        private static void RemovePlayers()
        {
            foreach (var player in allPlayers)
            {
                players.TryGetValue(player, out NetIkData netIkData);
                if (player == null || netIkData == null || netIkData.animator == null || netIkData.puppetMaster == null)
                {
                    removePlayers.Add(player);
                }
            }
            if (removePlayers.Count == 0)
                return;
            foreach (var item in removePlayers)
            {
                players.Remove(item);
                allPlayers.Remove(item);
            }
            removePlayers.Clear();
            rebuildTransformAccess = true;
        }
        public static void RemovePlayer(PuppetMaster player)
        {
            if (players.Remove(player))
            {
                if (player && player._animator)
                    puppetMasters.Remove(player._animator);
                rebuildTransformAccess = true;
            }
            allPlayers.Remove(player);
        }
        public static void EndProcessing()
        {
            writeStarted = true;
            if (playersProcessed == playerCount || doneProcessing.Wait(2))
            {
                UpdateArrays();
                writeJobHandle = new ApplyAllLocalTransforms(transformInfoInitArray).Schedule(transformsAccess);
                playersProcessed = 0;
            }
        }
        private static void UpdateArrays()
        {
            if (!rebuildTransformAccess && transformInfoInitArray.IsCreated && transformsAccess.isCreated && allPlayers.Count * BoneCount == transformInfoInitArray.Length)
            {
                int index = 0;
                foreach (var item in players)
                {
                    NativeArray<TransformInfoInit>.Copy(item.Value.transformInfos, 0, transformInfoInitArray, index, item.Value.transformInfos.Length);
                    index += item.Value.transformInfos.Length;
                }
                return;
            }
            //MelonLogger.Msg($"UpdateArrays: {rebuildTransformAccess}, {transformInfoInitArray.IsCreated} {transformsAccess.isCreated} {allPlayers.Count}, {BoneCount}, {allPlayers.Count * BoneCount} {transformInfoInitArray.Length}");
            transformsAccessList.Clear();
            transformInfoInitList.Clear();
            foreach (var item in players.Values)
            {
                transformInfoInitList.AddRange(item.transformInfos);
                transformsAccessList.AddRange(item.rotTransforms);
            }
            if (transformInfoInitArray.IsCreated)
                transformInfoInitArray.Dispose();
            transformInfoInitArray = transformInfoInitList.ToNativeArray(Allocator.Persistent);
            if (transformsAccess.isCreated)
                transformsAccess.SetTransforms(transformsAccessList.ToArray());
            else
                transformsAccess = new TransformAccessArray(transformsAccessList.ToArray());
            rebuildTransformAccess = false;
            transformsAccessList.Clear();
            transformInfoInitList.Clear();
        }
        private static void UpdateAll()
        {
            RemovePlayers();
            var time = Time.time;
            foreach (var item in players)
                UpdatePlayer(item.Value, time, staticMuscles);
            UpdateArrays();
            writeJobHandle = new ApplyAllLocalTransforms(transformInfoInitArray).Schedule(transformsAccess);
        }
        private static void UpdatePlayer(NetIkData netIkData, float time, float[] muscles)
        {
            var puppetMaster = netIkData.puppetMaster;
            Vector3 rot;
            if (puppetMaster._lastUpdate != netIkData.updateCurr)
            {
                // new data in current slot
                if (puppetMaster._lastBeforeUpdate == netIkData.updateCurr)
                {
                    // move current to previous slot
                    netIkData.dataPrev = netIkData.dataCurr;
                    netIkData.updatePrev = netIkData.updateCurr;
                    var tempRot = netIkData.rotations1;
                    netIkData.rotations1 = netIkData.rotations2;
                    netIkData.rotations2 = tempRot;

                    netIkData.hipsRot2 = netIkData.hipsRot1;
                    netIkData.rootRot2 = netIkData.rootRot1;
                    netIkData.hipsPos2 = netIkData.hipsPos1;
                    netIkData.rootPos2 = netIkData.rootPos1;
                }
                else
                {
                    netIkData.dataPrev = puppetMaster._playerAvatarMovementDataPast;
                    netIkData.updatePrev = puppetMaster._lastBeforeUpdate;

                    rot = netIkData.dataPrev.BodyRotation;
                    if (puppetMaster._isBlocked && !puppetMaster._isBlockedAlt)
                        rot -= netIkData.dataPrev.RelativeHipRotation - puppetMaster.relativeHipRotation;
                    netIkData.hipsRot2 = Quaternion.Euler(rot);

                    netIkData.rootRot2 = Quaternion.Euler(netIkData.dataPrev.RootRotation);
                    netIkData.hipsPos2 = netIkData.dataPrev.BodyPosition;
                    netIkData.rootPos2 = netIkData.dataPrev.RootPosition;
                    SetMuscleValues(muscles, netIkData.dataPrev);
                    for (int i = 0; i < netIkData.rotations2.Length; i++)
                    {
                        netIkData.rotations2[i] = GetBoneRotation(netIkData.boneElements[i], muscles);
                    }
                    FixBoneTwist(netIkData.rotations2, netIkData.boneElements, muscles);
                }
                netIkData.updateCurr = puppetMaster._lastUpdate;
                netIkData.dataCurr = puppetMaster._playerAvatarMovementDataCurrent;
                rot = netIkData.dataCurr.BodyRotation;
                if (puppetMaster._isBlocked && !puppetMaster._isBlockedAlt)
                    rot -= netIkData.dataPrev.RelativeHipRotation - puppetMaster.relativeHipRotation;
                netIkData.hipsRot1 = Quaternion.Euler(rot);

                netIkData.rootRot1 = Quaternion.Euler(netIkData.dataCurr.RootRotation);
                netIkData.hipsPos1 = netIkData.dataCurr.BodyPosition;
                netIkData.rootPos1 = netIkData.dataCurr.RootPosition;
                SetMuscleValues(muscles, netIkData.dataCurr);
                for (int i = 0; i < netIkData.rotations1.Length; i++)
                {
                    netIkData.rotations1[i] = GetBoneRotation(netIkData.boneElements[i], muscles);
                }
                FixBoneTwist(netIkData.rotations1, netIkData.boneElements, muscles);
            }
            // interpolate rotations

            var t = Mathf.Min((time - puppetMaster._lastUpdate) / puppetMaster.UpdateIntervalCalculated, 1f); // progress
            netIkData.hipsRotInterpolated = Quaternion.Slerp(netIkData.hipsRot2, netIkData.hipsRot1, t);
            netIkData.rootRotInterpolated = Quaternion.Slerp(netIkData.rootRot2, netIkData.rootRot1, t);
            netIkData.hipsPosInterpolated = Vector3.Lerp(netIkData.hipsPos2, netIkData.hipsPos1, t);
            netIkData.rootPosInterpolated = Vector3.Lerp(netIkData.rootPos2, netIkData.rootPos1, t);
            if (netIkData.dataCurr.IndexUseIndividualFingers)
            {
                for (int i = 1; i < netIkData.transformInfos.Length; i++)
                {
                    netIkData.transformInfos[i].initLocalRotation = math.slerp(netIkData.rotations2[i], netIkData.rotations1[i], t);
                    if (!netIkData.transformInfos[i].IsTransform)
                        netIkData.transformInfos[i].IsTransform = true;
                }
            }
            else
            {
                for (int i = 1; i < netIkData.transformInfos.Length; i++)
                {
                    if (i < (int)HumanBodyBones.LeftThumbProximal || i == 54)
                        netIkData.transformInfos[i].initLocalRotation = math.slerp(netIkData.rotations2[i], netIkData.rotations1[i], t);
                    else if (netIkData.transformInfos[i].IsTransform)
                        netIkData.transformInfos[i].IsTransform = false;
                }
            }
        }
        private static void FixBoneTwist(quaternion[] rotations, BoneElement[] boneElements, float[] muscles)
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
        static void FixLimbs(quaternion[] rotations, float upperMuscle, float middleMuscle, BoneElement boneElementUpper,
            BoneElement boneElementMiddle, int startIndex)
        {
            upperMuscle *= boneElementUpper.twistValue;
            var scale = upperMuscle >= 0 ? boneElementUpper.max.x : boneElementUpper.minAbs.x;
            var angleUpper = boneElementUpper.sign.x * scale * upperMuscle;
            var twistUpper = Quaternion.Euler(angleUpper, 0f, 0f);
            var rot = boneElementUpper.preQInv * rotations[startIndex] * boneElementUpper.postQ;
            rot = boneElementUpper.preQ * rot * Quaternion.Inverse(twistUpper) * boneElementUpper.postQInv;
            rotations[startIndex] = rot;

            middleMuscle *= 1f - boneElementMiddle.twistValue; // 1f - x ? idk
            var scaleMiddle = middleMuscle >= 0 ? boneElementMiddle.max.x : boneElementMiddle.minAbs.x;
            var angleMiddle = boneElementMiddle.sign.x * scaleMiddle * middleMuscle;
            var rotMiddleEuler = ((Quaternion)rotations[startIndex + 2]).eulerAngles;
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
        private static Quaternion GetBoneRotation(BoneElement boneElement, float[] muscles)
        {
            var dof = boneElement.dofExists;
            if (!dof.w)
                return Quaternion.identity;
            var id = boneElement.muscleIds;
            var rawMuscleValue = new Vector3(dof.x ? muscles[id.x] : 0f, dof.y ? muscles[id.y] : 0f, dof.z ? muscles[id.z] : 0f);
            if (boneElement.mixIndex > 0)
                rawMuscleValue.x *= boneElement.twistValue;

            var scale = new Vector3(
                dof.x ? (rawMuscleValue.x >= 0f ? boneElement.max.x : boneElement.minAbs.x) : 0f,
                dof.y ? (rawMuscleValue.y >= 0f ? boneElement.max.y : boneElement.minAbs.y) : 0f,
                dof.z ? (rawMuscleValue.z >= 0f ? boneElement.max.z : boneElement.minAbs.z) : 0f);
            var _angle = Vector3.Scale(boneElement.sign, Vector3.Scale(scale, rawMuscleValue));
            var twist = Quaternion.Euler(_angle.x, 0f, 0f);
            var rotY = Mathf.Tan(Mathf.Deg2Rad * _angle.y * 0.5f);
            var rotZ = Mathf.Tan(Mathf.Deg2Rad * _angle.z * 0.5f);
            var tPoseQ = new Quaternion(0f, rotY, rotZ, 1f);
            tPoseQ.Normalize();
            tPoseQ *= twist;
            var result = boneElement.preQ * tPoseQ * boneElement.postQInv;
            return result;
        }
        internal static void SetMuscleValues(float[] muscles, PlayerAvatarMovementData data)
        {
            muscles[(int)MuscleNamesEnum.SpineFrontBack] = data.SpineFrontBack;
            muscles[(int)MuscleNamesEnum.SpineLeftRight] = data.SpineLeftRight;
            muscles[(int)MuscleNamesEnum.SpineTwistLeftRight] = data.SpineTwistLeftRight;
            muscles[(int)MuscleNamesEnum.ChestFrontBack] = data.ChestFrontBack;
            muscles[(int)MuscleNamesEnum.ChestLeftRight] = data.ChestLeftRight;
            muscles[(int)MuscleNamesEnum.ChestTwistLeftRight] = data.ChestTwistLeftRight;
            muscles[(int)MuscleNamesEnum.UpperChestFrontBack] = data.UpperChestFrontBack;
            muscles[(int)MuscleNamesEnum.UpperChestLeftRight] = data.UpperChestLeftRight;
            muscles[(int)MuscleNamesEnum.UpperChestTwistLeftRight] = data.UpperChestTwistLeftRight;
            muscles[(int)MuscleNamesEnum.NeckNodDownUp] = data.NeckNodDownUp;
            muscles[(int)MuscleNamesEnum.NeckTiltLeftRight] = data.NeckTiltLeftRight;
            muscles[(int)MuscleNamesEnum.NeckTurnLeftRight] = data.NeckTurnLeftRight;
            muscles[(int)MuscleNamesEnum.HeadNodDownUp] = data.HeadNodDownUp;
            muscles[(int)MuscleNamesEnum.HeadTiltLeftRight] = data.HeadTiltLeftRight;
            muscles[(int)MuscleNamesEnum.HeadTurnLeftRight] = data.HeadTurnLeftRight;
            muscles[(int)MuscleNamesEnum.LeftUpperLegFrontBack] = data.LeftUpperLegFrontBack;
            muscles[(int)MuscleNamesEnum.LeftUpperLegInOut] = data.LeftUpperLegInOut;
            muscles[(int)MuscleNamesEnum.LeftUpperLegTwistInOut] = data.LeftUpperLegTwistInOut;
            muscles[(int)MuscleNamesEnum.LeftLowerLegStretch] = data.LeftLowerLegStretch;
            muscles[(int)MuscleNamesEnum.LeftLowerLegTwistInOut] = data.LeftLowerLegTwistInOut;
            muscles[(int)MuscleNamesEnum.LeftFootUpDown] = data.LeftFootUpDown;
            muscles[(int)MuscleNamesEnum.LeftFootTwistInOut] = data.LeftFootTwistInOut;
            muscles[(int)MuscleNamesEnum.LeftToesUpDown] = data.LeftToesUpDown;
            muscles[(int)MuscleNamesEnum.RightUpperLegFrontBack] = data.RightUpperLegFrontBack;
            muscles[(int)MuscleNamesEnum.RightUpperLegInOut] = data.RightUpperLegInOut;
            muscles[(int)MuscleNamesEnum.RightUpperLegTwistInOut] = data.RightUpperLegTwistInOut;
            muscles[(int)MuscleNamesEnum.RightLowerLegStretch] = data.RightLowerLegStretch;
            muscles[(int)MuscleNamesEnum.RightLowerLegTwistInOut] = data.RightLowerLegTwistInOut;
            muscles[(int)MuscleNamesEnum.RightFootUpDown] = data.RightFootUpDown;
            muscles[(int)MuscleNamesEnum.RightFootTwistInOut] = data.RightFootTwistInOut;
            muscles[(int)MuscleNamesEnum.RightToesUpDown] = data.RightToesUpDown;
            muscles[(int)MuscleNamesEnum.LeftShoulderDownUp] = data.LeftShoulderDownUp;
            muscles[(int)MuscleNamesEnum.LeftShoulderFrontBack] = data.LeftShoulderFrontBack;
            muscles[(int)MuscleNamesEnum.LeftArmDownUp] = data.LeftArmDownUp;
            muscles[(int)MuscleNamesEnum.LeftArmFrontBack] = data.LeftArmFrontBack;
            muscles[(int)MuscleNamesEnum.LeftArmTwistInOut] = data.LeftArmTwistInOut;
            muscles[(int)MuscleNamesEnum.LeftForearmStretch] = data.LeftForearmStretch;
            muscles[(int)MuscleNamesEnum.LeftForearmTwistInOut] = data.LeftForearmTwistInOut;
            muscles[(int)MuscleNamesEnum.LeftHandDownUp] = data.LeftHandDownUp;
            muscles[(int)MuscleNamesEnum.LeftHandInOut] = data.LeftHandInOut;
            muscles[(int)MuscleNamesEnum.RightShoulderDownUp] = data.RightShoulderDownUp;
            muscles[(int)MuscleNamesEnum.RightShoulderFrontBack] = data.RightShoulderFrontBack;
            muscles[(int)MuscleNamesEnum.RightArmDownUp] = data.RightArmDownUp;
            muscles[(int)MuscleNamesEnum.RightArmFrontBack] = data.RightArmFrontBack;
            muscles[(int)MuscleNamesEnum.RightArmTwistInOut] = data.RightArmTwistInOut;
            muscles[(int)MuscleNamesEnum.RightForearmStretch] = data.RightForearmStretch;
            muscles[(int)MuscleNamesEnum.RightForearmTwistInOut] = data.RightForearmTwistInOut;
            muscles[(int)MuscleNamesEnum.RightHandDownUp] = data.RightHandDownUp;
            muscles[(int)MuscleNamesEnum.RightHandInOut] = data.RightHandInOut;
            if (data.IndexUseIndividualFingers)
            {
                muscles[(int)MuscleNamesEnum.LeftThumb1Stretched] = data.LeftThumbCurl;
                muscles[(int)MuscleNamesEnum.LeftThumb2Stretched] = data.LeftThumbCurl;
                muscles[(int)MuscleNamesEnum.LeftThumb3Stretched] = data.LeftThumbCurl;
                muscles[(int)MuscleNamesEnum.LeftIndex1Stretched] = data.LeftIndexCurl;
                muscles[(int)MuscleNamesEnum.LeftIndex2Stretched] = data.LeftIndexCurl;
                muscles[(int)MuscleNamesEnum.LeftIndex3Stretched] = data.LeftIndexCurl;
                muscles[(int)MuscleNamesEnum.LeftMiddle1Stretched] = data.LeftMiddleCurl;
                muscles[(int)MuscleNamesEnum.LeftMiddle2Stretched] = data.LeftMiddleCurl;
                muscles[(int)MuscleNamesEnum.LeftMiddle3Stretched] = data.LeftMiddleCurl;
                muscles[(int)MuscleNamesEnum.LeftRing1Stretched] = data.LeftRingCurl;
                muscles[(int)MuscleNamesEnum.LeftRing2Stretched] = data.LeftRingCurl;
                muscles[(int)MuscleNamesEnum.LeftRing3Stretched] = data.LeftRingCurl;
                muscles[(int)MuscleNamesEnum.LeftLittle1Stretched] = data.LeftPinkyCurl;
                muscles[(int)MuscleNamesEnum.LeftLittle2Stretched] = data.LeftPinkyCurl;
                muscles[(int)MuscleNamesEnum.LeftLittle3Stretched] = data.LeftPinkyCurl;
                muscles[(int)MuscleNamesEnum.RightThumb1Stretched] = data.RightThumbCurl;
                muscles[(int)MuscleNamesEnum.RightThumb2Stretched] = data.RightThumbCurl;
                muscles[(int)MuscleNamesEnum.RightThumb3Stretched] = data.RightThumbCurl;
                muscles[(int)MuscleNamesEnum.RightIndex1Stretched] = data.RightIndexCurl;
                muscles[(int)MuscleNamesEnum.RightIndex2Stretched] = data.RightIndexCurl;
                muscles[(int)MuscleNamesEnum.RightIndex3Stretched] = data.RightIndexCurl;
                muscles[(int)MuscleNamesEnum.RightMiddle1Stretched] = data.RightMiddleCurl;
                muscles[(int)MuscleNamesEnum.RightMiddle2Stretched] = data.RightMiddleCurl;
                muscles[(int)MuscleNamesEnum.RightMiddle3Stretched] = data.RightMiddleCurl;
                muscles[(int)MuscleNamesEnum.RightRing1Stretched] = data.RightRingCurl;
                muscles[(int)MuscleNamesEnum.RightRing2Stretched] = data.RightRingCurl;
                muscles[(int)MuscleNamesEnum.RightRing3Stretched] = data.RightRingCurl;
                muscles[(int)MuscleNamesEnum.RightLittle1Stretched] = data.RightPinkyCurl;
                muscles[(int)MuscleNamesEnum.RightLittle2Stretched] = data.RightPinkyCurl;
                muscles[(int)MuscleNamesEnum.RightLittle3Stretched] = data.RightPinkyCurl;
                muscles[56] = muscles[76] = 0.3f;   //thumbs splay
            }
        }
    }
}
