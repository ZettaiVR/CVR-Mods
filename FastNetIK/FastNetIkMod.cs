using MelonLoader;
using HarmonyLib;
using ABI_RC.Core.Player;
using UnityEngine;

[assembly: MelonInfo(typeof(Zettai.FastNetIkMod), "FastNetIkMod", "1.2", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class FastNetIkMod : MelonMod
    {
        private static MelonPreferences_Entry<bool> netIk;
        private static MelonPreferences_Entry<bool> netIkDeserialize;
        private static MelonPreferences_Entry<bool> netIkSerializeSpread;
        private static MelonPreferences_Entry<float> netIkThumbsSplay;
        private static MelonPreferences_Entry<float> netIkIndexSplay;
        private static MelonPreferences_Entry<float> netIkMiddleSplay;
        private static MelonPreferences_Entry<float> netIkRingSplay;
        private static MelonPreferences_Entry<float> netIkLittleSplay;
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            netIk = category.CreateEntry("FastNetIK", true, "FastNetIK enable");
            netIkDeserialize = category.CreateEntry("FastNetIKdeserializer", true, "FastNetIK deserializer");
            netIkSerializeSpread = category.CreateEntry("FastNetIkSerializeSpread", true, "FastNetIK send finger spread");

            netIkThumbsSplay = category.CreateEntry("netIkThumbsSplay", 0.3f, "Thumb spread (-1..1)");
            netIkIndexSplay = category.CreateEntry("netIkIndexSplay", 0f, "Index finger spread (-1..1)");
            netIkMiddleSplay = category.CreateEntry("netIkMiddleSplay", 0f, "Middle finger spread (-1..1)");
            netIkRingSplay = category.CreateEntry("netIkRingSplay", 0f, "Ring finger spread (-1..1)");
            netIkLittleSplay = category.CreateEntry("netIkLittleSplay", 0f, "Little finger spread (-1..1)");

            netIkThumbsSplay.OnValueChanged += NetIkSplay_OnValueChanged;
            netIkIndexSplay.OnValueChanged += NetIkSplay_OnValueChanged;
            netIkMiddleSplay.OnValueChanged += NetIkSplay_OnValueChanged;
            netIkRingSplay.OnValueChanged += NetIkSplay_OnValueChanged;
            netIkLittleSplay.OnValueChanged += NetIkSplay_OnValueChanged;
            Setup.Init(); 
        }
      

        private void NetIkSplay_OnValueChanged(float arg1, float arg2)
        {
            var thumb = netIkThumbsSplay.Value = Mathf.Clamp(netIkThumbsSplay.Value, -1f, 1f);
            var index = netIkIndexSplay.Value = Mathf.Clamp(netIkIndexSplay.Value, -1f, 1f);
            var middle = netIkMiddleSplay.Value = Mathf.Clamp(netIkMiddleSplay.Value, -1f, 1f);
            var ring = netIkRingSplay.Value = Mathf.Clamp(netIkRingSplay.Value, -1f, 1f);
            var little = netIkLittleSplay.Value = Mathf.Clamp(netIkLittleSplay.Value, -1f, 1f);
            NetIkUpdate.UpdateFingerSpread(thumb, index, middle, ring, little);
        }


        [HarmonyPatch(typeof(ABI_RC.Core.Networking.Jobs.NetworkRootDataUpdate))]
        class Network
        {
            [HarmonyPatch(nameof(ABI_RC.Core.Networking.Jobs.NetworkRootDataUpdate.Apply))]
            [HarmonyPrefix]
            static bool Prefix(DarkRift.Message message)
            {
                if (!netIk.Value || !netIkDeserialize.Value)
                    return true;
                ReadNetworkData.AddData(message);
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerAvatarMovementData))]
        class SendFingerSpread
        {
            [HarmonyPatch(nameof(PlayerAvatarMovementData.SetDataFromAnimator))]
            [HarmonyPostfix]
            static void Postfix(PlayerAvatarMovementData __instance)
            {
                if (!netIk.Value || !netIkSerializeSpread.Value || !ABI_RC.Systems.IK.IKSystem.Instance.FingerSystem.controlActive)
                    return;

                var muscles = ABI_RC.Systems.IK.IKSystem.Instance.humanPose.muscles;
                if (muscles == null)
                    return;

                __instance.LeftThumbSpread    = muscles[56] + 10f;
                __instance.LeftIndexSpread    = muscles[60];
                __instance.LeftMiddleSpread   = muscles[64];
                __instance.LeftRingSpread     = muscles[68];
                __instance.LeftPinkySpread    = muscles[72];
                __instance.RightThumbSpread   = muscles[76];
                __instance.RightIndexSpread   = muscles[80];
                __instance.RightMiddleSpread  = muscles[84];
                __instance.RightRingSpread    = muscles[88];
                __instance.RightPinkySpread   = muscles[92];
            }
            [HarmonyPatch(nameof(PlayerAvatarMovementData.CopyDataFrom))]
            [HarmonyPostfix]
            static void PostfixCopy(PlayerAvatarMovementData __instance, PlayerAvatarMovementData source)
            {
                if (!netIk.Value || !netIkSerializeSpread.Value)
                    return;

                __instance.LeftIndexSpread = source.LeftIndexSpread;
                __instance.LeftMiddleSpread = source.LeftMiddleSpread;
                __instance.LeftRingSpread = source.LeftRingSpread;
                __instance.LeftPinkySpread = source.LeftPinkySpread;
                __instance.RightThumbSpread = source.RightThumbSpread;
                __instance.LeftThumbSpread = source.LeftThumbSpread;
                __instance.RightIndexSpread = source.RightIndexSpread;
                __instance.RightMiddleSpread = source.RightMiddleSpread;
                __instance.RightPinkySpread = source.RightPinkySpread;
                __instance.RightRingSpread = source.RightRingSpread;
            }
        }

        [HarmonyPatch(typeof(ABI_RC.Systems.MovementSystem.MovementSystem), nameof(ABI_RC.Systems.MovementSystem.MovementSystem.Update))]
        class MovementSystem
        {
            static void Prefix()
            {
                if (!netIk.Value)
                    return;
                NetIkUpdate.useFingerSpread = netIkSerializeSpread.Value;
                ReadNetworkData.StartProcessing();
                NetIkUpdate.StartProcessing();
                Unity.Jobs.JobHandle.ScheduleBatchedJobs();
            }
        }
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ReadNetworkData.ClearAllCachedData();
        }

        public override void OnLateUpdate()
        {
            if (!netIk.Value)
                return;
            NetIkUpdate.StartWriteJobs();
        }
        [HarmonyPatch(typeof(PuppetMaster), nameof(PuppetMaster.Update))]
        class PuppetMasterUpdate
        {
            static void Prefix()
            {
                if (!netIk.Value)
                    return;
                ReadNetworkData.CompleteProcessing();
            }
        }
        [HarmonyPatch(typeof(PuppetMaster), nameof(PuppetMaster.AvatarInstantiated))]
        class PuppetMasterAvatarInstantiated
        {
            static void Postfix(PuppetMaster __instance)
            {
                if (!string.IsNullOrEmpty(__instance._playerDescriptor.ownerId))
                {
                    Setup.AddPlayer(__instance);
                    __instance._playerAvatarMovementDataCurrent._applyPosePast = default(HumanPose);
                    __instance._playerAvatarMovementDataPast._applyPosePast = default(HumanPose);
                }
            }
        }
        [HarmonyPatch(typeof(PuppetMaster), nameof(PuppetMaster.AvatarDestroyed))]
        class PuppetMasterAvatarDestroyed
        {
            static void Postfix(PuppetMaster __instance)
            {
                NetIkUpdate.RemovePlayer(__instance);
            }
        }
        [HarmonyPatch(typeof(PlayerAvatarMovementData), nameof(PlayerAvatarMovementData.WriteDataToAnimatorLerped))]
        class NetIkPatch
        {
            static bool Prefix(PlayerAvatarMovementData __instance, Animator animator, Vector3 relativeHipRotation, bool isBlocked, bool isBlockedAlt, PlayerAvatarMovementData previousData, float progress)
            {
                if (!netIk.Value || animator == null || previousData == null)
                    return true;
                return false;
            }
        }
    }
}
