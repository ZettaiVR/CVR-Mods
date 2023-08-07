using MelonLoader;
using HarmonyLib;
using ABI_RC.Core.Player;
using UnityEngine;

[assembly: MelonInfo(typeof(Zettai.FastNetIkMod), "FastNetIkMod", "1.3", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class FastNetIkMod : MelonMod
    {
        private static MelonPreferences_Entry<bool> netIk;
        private static MelonPreferences_Entry<bool> netIkDeserialize;
       
        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            netIk = category.CreateEntry("FastNetIK", true, "FastNetIK enable");
            netIkDeserialize = category.CreateEntry("FastNetIKdeserializer", true, "FastNetIK deserializer");

            Setup.Init(); 
        }
        [HarmonyPatch(typeof(ABI_RC.Core.Networking.Jobs.NetworkRootDataUpdate))]
        class Network
        {
            [HarmonyPatch(nameof(ABI_RC.Core.Networking.Jobs.NetworkRootDataUpdate.Apply))]
            [HarmonyPrefix]
            static bool PrefixApply(DarkRift.Message message)
            {
                if (!netIk.Value || !netIkDeserialize.Value)
                    return true;
                ReadNetworkData.AddData(message);
                return false;
            }
        }

        [HarmonyPatch(typeof(ABI_RC.Systems.MovementSystem.MovementSystem), nameof(ABI_RC.Systems.MovementSystem.MovementSystem.Update))]
        class MovementSystem
        {
            static void Prefix()
            {
                if (!netIk.Value)
                    return;
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
                if (!netIk.Value || !ReadNetworkData.started)
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
                    __instance._playerAvatarMovementDataCurrent._applyPosePast = default;
                    __instance._playerAvatarMovementDataPast._applyPosePast = default;
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
