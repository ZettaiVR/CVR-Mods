using ABI_RC.Core.Player;
using MagicaCloth;
using MelonLoader;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Zettai
{
    class Update
    {
        internal static JobHandle processingJobHandle;
        internal static JobHandle copyJobHandle;
        public static JobHandle writeJobHandle;
        public static JobHandle writeJobHandleRoot;
        public static JobHandle writeJobHandleHips;
        private static bool rebuildTransformAccess = true;
        internal static bool useFingerSpread;
        private static bool initDone = false;
        private static float ThumbsSplay = 0.3f;
        private static float IndexSplay = 0f;
        private static float MiddleSplay = 0f;
        private static float RingSplay = 0f;
        private static float PinkySplay = 0f;
        
   //     internal static volatile bool Test = false;
        private static readonly byte[] boneFlagArray = new byte[256];
        private static readonly int[] writeBoneIndexArray = new int[256];
        private static readonly int[] boneParentIndexArray = new int[256];
        private static readonly short[] boneUnityPhysicsArray = new short[256];
        private static readonly int BoneCount = (int)HumanBodyBones.LastBone;
        internal static readonly List<TransformInfoInit> transformInfoInitList = new List<TransformInfoInit>();
        internal static readonly List<Transform> transformsAccessList = new List<Transform>();
        internal static readonly List<Transform> transformsAccessListRoot = new List<Transform>();
        internal static readonly List<Transform> transformsAccessListHips = new List<Transform>();
        private static readonly HashSet<PuppetMaster> removePlayers = new HashSet<PuppetMaster>();
        internal static readonly HashSet<PuppetMaster> allPlayers = new HashSet<PuppetMaster>();
        internal static readonly Dictionary<PuppetMaster, NetIkData> players = new Dictionary<PuppetMaster, NetIkData>();
        internal static readonly Dictionary<Animator, PuppetMaster> puppetMasters = new Dictionary<Animator, PuppetMaster>();
        private static readonly List<PuppetMaster> playersToProcessList = new List<PuppetMaster>(256);
        private static NativeArray<TransformInfoInit> transformInfoInitArray;
        private static NativeArray<byte>  boneFlagList;
        private static NativeArray<int>   writeBoneIndexList;
        private static NativeArray<int>   boneParentIndexList;
        private static NativeArray<short> boneUnityPhysicsList;
        private static NativeList<float3> writeBonePosListRoot;
        private static NativeList<quaternion> writeBoneRotListRoot;
        private static NativeList<float3> writeBonePosListHips;
        private static NativeList<quaternion> writeBoneRotListHips;
        private static TransformAccessArray rootArray;
        private static TransformAccessArray hipsArray;
        private static TransformAccessArray transformsAccess;

        struct NetIkProcessingJob : IJobParallelFor
        {
            private readonly float time;
            public NetIkProcessingJob(float time)
            {
                this.time = time;
            }
            public void Execute(int index)
            {
                var player = playersToProcessList[index];
                if (players.TryGetValue(player, out var data))
                    UpdatePlayer(data, time);
            }
        }
        struct NetIkCopyJob : IJob
        {
            public void Execute()
            {
                if (!rebuildTransformAccess &&
                transformInfoInitArray.IsCreated &&
                allPlayers.Count * BoneCount == transformInfoInitArray.Length &&
                writeBonePosListRoot.IsCreated &&
                writeBoneRotListRoot.IsCreated &&
                writeBonePosListHips.IsCreated &&
                writeBoneRotListHips.IsCreated)
                {
                    int index = 0;
                    int count = 0;
                    foreach (var item in players)
                    {
                        NativeArray<TransformInfoInit>.Copy(item.Value.transformInfos, 0, transformInfoInitArray, index, item.Value.transformInfos.Length);
                        index += item.Value.transformInfos.Length;
                        writeBonePosListRoot[count] = item.Value.rootPosInterpolated;
                        writeBoneRotListRoot[count] = item.Value.rootRotInterpolated;
                        writeBonePosListHips[count] = item.Value.hipsPosInterpolated;
                        writeBoneRotListHips[count++] = item.Value.hipsRotInterpolated;
                    }
                }
            }
        }

        internal static void UpdateFingerSpread(float thumb, float index, float middle, float ring, float little) 
        {
            ThumbsSplay = thumb;
            IndexSplay = index;
            MiddleSplay = middle;
            RingSplay = ring;
            PinkySplay = little;
        }
        public static void StartProcessing()
        {
            RemovePlayers();
            playersToProcessList.Clear();
            playersToProcessList.AddRange(allPlayers);
            RebuildArraysIfNeeded();
            processingJobHandle = new NetIkProcessingJob(Time.time).Schedule(playersToProcessList.Count, 1);
            copyJobHandle = new NetIkCopyJob().Schedule(processingJobHandle);
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
        public static void StartJobs()
        {
            if (!initDone)
                RebuildArrays();
            ScheduleJobs();
        }
        private static void RebuildArraysIfNeeded()
        {
            if (rebuildTransformAccess ||
                    !transformInfoInitArray.IsCreated ||
                    allPlayers.Count * BoneCount != transformInfoInitArray.Length ||
                    !transformsAccess.isCreated ||
                    !writeBonePosListRoot.IsCreated ||
                    !writeBoneRotListRoot.IsCreated ||
                    !writeBonePosListHips.IsCreated ||
                    !writeBoneRotListHips.IsCreated ||
                    !rootArray.isCreated ||
                    !hipsArray.isCreated)
            {
                RebuildArrays();
            }
        }
        private static void RebuildArrays() 
        {
            transformsAccessListRoot.Clear();
            transformsAccessListHips.Clear();
            transformsAccessList.Clear();
            transformInfoInitList.Clear();

            if (!writeBonePosListRoot.IsCreated) writeBonePosListRoot = new NativeList<float3>(players.Count, Allocator.Persistent);
            if (!writeBoneRotListRoot.IsCreated) writeBoneRotListRoot = new NativeList<quaternion>(players.Count, Allocator.Persistent);
            if (!writeBonePosListHips.IsCreated) writeBonePosListHips = new NativeList<float3>(players.Count, Allocator.Persistent);
            if (!writeBoneRotListHips.IsCreated) writeBoneRotListHips = new NativeList<quaternion>(players.Count, Allocator.Persistent);

            writeBonePosListRoot.Clear();
            writeBoneRotListRoot.Clear();
            writeBonePosListHips.Clear();
            writeBoneRotListHips.Clear();

            foreach (var item in players.Values)
            {
                transformInfoInitList.AddRange(item.transformInfos);
                transformsAccessList.AddRange(item.rotTransforms);
                transformsAccessListRoot.Add(item.root);
                transformsAccessListHips.Add(item.hips);
                writeBonePosListRoot.Add(item.rootPosInterpolated);
                writeBoneRotListRoot.Add(item.rootRotInterpolated);
                writeBonePosListHips.Add(item.hipsPosInterpolated);
                writeBoneRotListHips.Add(item.hipsRotInterpolated);
            }
            if (transformInfoInitArray.IsCreated)
                transformInfoInitArray.Dispose();
            transformInfoInitArray = transformInfoInitList.ToNativeArray(Allocator.Persistent);
            if (transformsAccess.isCreated)
                transformsAccess.SetTransforms(transformsAccessList.ToArray());
            else
                transformsAccess = new TransformAccessArray(transformsAccessList.ToArray());

            if (rootArray.isCreated)
                rootArray.SetTransforms(transformsAccessListRoot.ToArray());
            else
                rootArray = new TransformAccessArray(transformsAccessListRoot.ToArray());

            if (hipsArray.isCreated)
                hipsArray.SetTransforms(transformsAccessListHips.ToArray());
            else
                hipsArray = new TransformAccessArray(transformsAccessListHips.ToArray());

            rebuildTransformAccess = false;
            transformsAccessList.Clear();
            transformInfoInitList.Clear();
            transformsAccessListRoot.Clear();
            transformsAccessListHips.Clear();
            initDone = true;
        }
        internal static unsafe void ArrayInit()
        {
            // boneFlagList[num] = 32 = 0x20202020
            // writeBoneIndexList[index] > 0
            // boneParentIndexList[num] = -1  = 0xFFFFFFF
            // boneUnityPhysicsList = x
            fixed (byte* bufferPtr = boneFlagArray)
            {
                var longPtr = (ulong*)bufferPtr;
                var length = boneFlagArray.Length / 8;
                for (int i = 0; i < length; i++)
                {
                    longPtr[i] = 0x2020202020202020;
                }
            }
            fixed (int* writeBonePtr = writeBoneIndexArray)
            fixed (int* boneParentPtr = boneParentIndexArray)
            {
                var longPtr2 = (ulong*)writeBonePtr;
                var longPtr3 = (ulong*)boneParentPtr;
                var length = writeBoneIndexArray.Length / 2;
                for (int i = 0; i < length; i++)
                {
                    longPtr2[i] = 0x0000000200000002;
                    longPtr3[i] = 0xFFFFFFFFFFFFFFFF;
                }
            }
            if (!boneFlagList.IsCreated)
                boneFlagList = new NativeArray<byte>(boneFlagArray, Allocator.Persistent);
            if (!writeBoneIndexList.IsCreated)
                writeBoneIndexList = new NativeArray<int>(writeBoneIndexArray, Allocator.Persistent);
            if (!boneParentIndexList.IsCreated)
                boneParentIndexList = new NativeArray<int>(boneParentIndexArray, Allocator.Persistent);
            if (!boneUnityPhysicsList.IsCreated)
                boneUnityPhysicsList = new NativeArray<short>(boneUnityPhysicsArray, Allocator.Persistent);
            RebuildArrays();
        }
        private static void ScheduleJobs()
        {
            writeJobHandleRoot = new PhysicsManagerBoneData.WriteBontToTransformJob2()
            {
                fixedUpdateCount = 1,
                boneFlagList = boneFlagList,
                writeBoneIndexList = writeBoneIndexList,
                boneParentIndexList = boneParentIndexList,
                writeBonePosList = writeBonePosListRoot,
                writeBoneRotList = writeBoneRotListRoot,
                boneUnityPhysicsList = boneUnityPhysicsList
            }.Schedule(rootArray, copyJobHandle);
            writeJobHandleHips = new PhysicsManagerBoneData.WriteBontToTransformJob2()
            {
                fixedUpdateCount = 1,
                boneFlagList = boneFlagList,
                writeBoneIndexList = writeBoneIndexList,
                boneParentIndexList = boneParentIndexList,
                writeBonePosList = writeBonePosListHips,
                writeBoneRotList = writeBoneRotListHips,
                boneUnityPhysicsList = boneUnityPhysicsList
            }.Schedule(hipsArray, writeJobHandleRoot);
            writeJobHandle = new ApplyAllLocalTransforms(transformInfoInitArray).Schedule(transformsAccess, writeJobHandleHips);
        }
        private static void UpdatePlayer(NetIkData netIkData, float time)
        {
            var puppetMaster = netIkData.puppetMaster;
            Vector3 rot;
            if (puppetMaster._lastUpdate != netIkData.updateCurr)
                unsafe
                {
                    var muscles = stackalloc float[95];
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
                            netIkData.rotations2[i] = PoseHandling.GetBoneRotation(netIkData.boneElements[i], muscles);
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
                        netIkData.rotations1[i] = PoseHandling.GetBoneRotation(netIkData.boneElements[i], muscles);
                    PoseHandling.FixBoneTwist(netIkData.rotations1, netIkData.boneElements, muscles);
                }
            // interpolate rotations
            var t = Mathf.Min((time - puppetMaster._lastUpdate) / puppetMaster.UpdateIntervalCalculated, 1f); // progress
            netIkData.hipsRotInterpolated = Quaternion.Slerp(netIkData.hipsRot2, netIkData.hipsRot1, t);
            netIkData.rootRotInterpolated = Quaternion.Slerp(netIkData.rootRot2, netIkData.rootRot1, t);
            netIkData.hipsPosInterpolated = Vector3.Lerp(netIkData.hipsPos2, netIkData.hipsPos1, t);
            netIkData.rootPosInterpolated = Vector3.Lerp(netIkData.rootPos2, netIkData.rootPos1, t);

            for (int i = 1; i < 24; i++)
            {
                if (netIkData.transformInfos[i].IsTransform)
                    netIkData.transformInfos[i].initLocalRotation = Quaternion.Slerp(netIkData.rotations2[i], netIkData.rotations1[i], t);
            }
            if (netIkData.transformInfos[54].IsTransform)
                netIkData.transformInfos[54].initLocalRotation = Quaternion.Slerp(netIkData.rotations2[54], netIkData.rotations1[54], t);

            if (netIkData.dataCurr.IndexUseIndividualFingers)
            {
                bool setFingersOn = !netIkData.fingers;
                for (int i = 24; i < 54; i++)
                {
                    if (netIkData.transformInfos[i].IsTransform)
                        netIkData.transformInfos[i].initLocalRotation = Quaternion.Slerp(netIkData.rotations2[i], netIkData.rotations1[i], t);
                    if (setFingersOn)
                        netIkData.transformInfos[i].IsEnabled = true;
                }
                if (setFingersOn)
                    netIkData.fingers = true;
            }
            else if (netIkData.fingers)
            {
                for (int i = 24; i < 54; i++)
                    netIkData.transformInfos[i].IsEnabled = false;

                netIkData.fingers = false;
            }
        }
        internal static unsafe void SetMuscleValues(float* muscles, PlayerAvatarMovementData data)
        {
            muscles[0] = data.SpineFrontBack;
            muscles[1] = data.SpineLeftRight;
            muscles[2] = data.SpineTwistLeftRight;
            muscles[3] = data.ChestFrontBack;
            muscles[4] = data.ChestLeftRight;
            muscles[5] = data.ChestTwistLeftRight;
            muscles[6] = data.UpperChestFrontBack;
            muscles[7] = data.UpperChestLeftRight;
            muscles[8] = data.UpperChestTwistLeftRight;
            muscles[9] = data.NeckNodDownUp;
            muscles[10] = data.NeckTiltLeftRight;
            muscles[11] = data.NeckTurnLeftRight;
            muscles[12] = data.HeadNodDownUp;
            muscles[13] = data.HeadTiltLeftRight;
            muscles[14] = data.HeadTurnLeftRight;
            muscles[21] = data.LeftUpperLegFrontBack;
            muscles[22] = data.LeftUpperLegInOut;
            muscles[23] = data.LeftUpperLegTwistInOut;
            muscles[24] = data.LeftLowerLegStretch;
            muscles[25] = data.LeftLowerLegTwistInOut;
            muscles[26] = data.LeftFootUpDown;
            muscles[27] = data.LeftFootTwistInOut;
            muscles[28] = data.LeftToesUpDown;
            muscles[29] = data.RightUpperLegFrontBack;
            muscles[30] = data.RightUpperLegInOut;
            muscles[31] = data.RightUpperLegTwistInOut;
            muscles[32] = data.RightLowerLegStretch;
            muscles[33] = data.RightLowerLegTwistInOut;
            muscles[34] = data.RightFootUpDown;
            muscles[35] = data.RightFootTwistInOut;
            muscles[36] = data.RightToesUpDown;
            muscles[37] = data.LeftShoulderDownUp;
            muscles[38] = data.LeftShoulderFrontBack;
            muscles[39] = data.LeftArmDownUp;
            muscles[40] = data.LeftArmFrontBack;
            muscles[41] = data.LeftArmTwistInOut;
            muscles[42] = data.LeftForearmStretch;
            muscles[43] = data.LeftForearmTwistInOut;
            muscles[44] = data.LeftHandDownUp;
            muscles[45] = data.LeftHandInOut;
            muscles[46] = data.RightShoulderDownUp;
            muscles[47] = data.RightShoulderFrontBack;
            muscles[48] = data.RightArmDownUp;
            muscles[49] = data.RightArmFrontBack;
            muscles[50] = data.RightArmTwistInOut;
            muscles[51] = data.RightForearmStretch;
            muscles[52] = data.RightForearmTwistInOut;
            muscles[53] = data.RightHandDownUp;
            muscles[54] = data.RightHandInOut;
            if (!data.IndexUseIndividualFingers)
                return;

            muscles[55] = muscles[57] = muscles[58] = 0.7f + -1.7f * data.LeftThumbCurl;
            muscles[59] = muscles[61] = muscles[62] = 0.7f + -1.7f * data.LeftIndexCurl;
            muscles[63] = muscles[65] = muscles[66] = 0.7f + -1.7f * data.LeftMiddleCurl;
            muscles[67] = muscles[69] = muscles[70] = 0.7f + -1.7f * data.LeftRingCurl;
            muscles[71] = muscles[73] = muscles[74] = 0.7f + -1.7f * data.LeftPinkyCurl;
            muscles[75] = muscles[77] = muscles[78] = 0.7f + -1.7f * data.RightThumbCurl;
            muscles[79] = muscles[81] = muscles[82] = 0.7f + -1.7f * data.RightIndexCurl;
            muscles[83] = muscles[85] = muscles[86] = 0.7f + -1.7f * data.RightMiddleCurl;
            muscles[87] = muscles[89] = muscles[90] = 0.7f + -1.7f * data.RightRingCurl;
            muscles[91] = muscles[93] = muscles[94] = 0.7f + -1.7f * data.RightPinkyCurl;


            float leftThumbSpread = data.LeftThumbSpread;

            if (!useFingerSpread || leftThumbSpread < 5f)
            {
                muscles[56] = muscles[76] = ThumbsSplay;
                muscles[60] = muscles[80] = IndexSplay;
                muscles[64] = muscles[84] = MiddleSplay;
                muscles[68] = muscles[88] = RingSplay;
                muscles[72] = muscles[92] = PinkySplay;
                return;
            }

            // leftThumbSpread is offset by 10 so we can recognize if it's sent by this mod

            muscles[56] = leftThumbSpread - 10f;
            muscles[60] = data.LeftIndexSpread;
            muscles[64] = data.LeftMiddleSpread;
            muscles[68] = data.LeftRingSpread;
            muscles[72] = data.LeftPinkySpread;
            muscles[76] = data.RightThumbSpread;
            muscles[80] = data.RightIndexSpread;
            muscles[84] = data.RightMiddleSpread;
            muscles[88] = data.RightRingSpread;
            muscles[92] = data.RightPinkySpread;
        }
    }
}