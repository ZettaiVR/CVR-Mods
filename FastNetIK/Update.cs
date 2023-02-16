using ABI_RC.Core.Player;
using MelonLoader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                        if (!playersToProcess.TryDequeue(out var player))
                            continue;

                        if (players.TryGetValue(player, out var data))
                        {
                            UpdatePlayer(data, time, muscles);
                        }
                        Interlocked.Increment(ref playersProcessed);
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
            if (playersProcessed != playerCount)
            {
                MelonLogger.Msg("EndProcessing - Waiting");
                doneProcessing.Wait(2);
            }
            playersProcessed = 0;
        }
        public static void StartJobs()
        {
            UpdateArrays();
            writeJobHandle = new ApplyAllLocalTransforms(transformInfoInitArray).Schedule(transformsAccess);
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
                        netIkData.rotations2[i] = PoseHandling.GetBoneRotation(netIkData.boneElements[i], muscles);
                    }
                    PoseHandling.FixBoneTwist(netIkData.rotations2, netIkData.boneElements, muscles);
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
                    netIkData.rotations1[i] = PoseHandling.GetBoneRotation(netIkData.boneElements[i], muscles);
                }
                PoseHandling.FixBoneTwist(netIkData.rotations1, netIkData.boneElements, muscles);
            }
            // interpolate rotations

            var t = math.min((time - puppetMaster._lastUpdate) / puppetMaster.UpdateIntervalCalculated, 1f); // progress
            netIkData.hipsRotInterpolated = math.slerp(netIkData.hipsRot2, netIkData.hipsRot1, t);
            netIkData.rootRotInterpolated = math.slerp(netIkData.rootRot2, netIkData.rootRot1, t);
            netIkData.hipsPosInterpolated = math.lerp(netIkData.hipsPos2, netIkData.hipsPos1, t);
            netIkData.rootPosInterpolated = math.lerp(netIkData.rootPos2, netIkData.rootPos1, t);

            for (int i = 1; i < 24; i++)
            {
                if (netIkData.transformInfos[i].IsTransform)
                    netIkData.transformInfos[i].initLocalRotation = math.slerp(netIkData.rotations2[i], netIkData.rotations1[i], t);
            }
            if (netIkData.transformInfos[54].IsTransform)
                netIkData.transformInfos[54].initLocalRotation = math.slerp(netIkData.rotations2[54], netIkData.rotations1[54], t);

            if (netIkData.dataCurr.IndexUseIndividualFingers)
            {
                bool setFingersOn = !netIkData.fingers;
                for (int i = 24; i < 54; i++)
                {
                    if (netIkData.transformInfos[i].IsTransform)
                        netIkData.transformInfos[i].initLocalRotation = math.slerp(netIkData.rotations2[i], netIkData.rotations1[i], t);
                    if (setFingersOn)
                        netIkData.transformInfos[i].IsEnabled = true;
                }
                if (setFingersOn)
                    netIkData.fingers = true;
            }
            else if (netIkData.fingers)
            {
                for (int i = 24; i < 54; i++)
                {
                    netIkData.transformInfos[i].IsEnabled = false;
                }
                netIkData.fingers = false;
            }
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
