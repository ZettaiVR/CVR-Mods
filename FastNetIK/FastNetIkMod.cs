using MelonLoader;
using HarmonyLib;
using ABI_RC.Core.Player;
using UnityEngine;

[assembly: MelonInfo(typeof(Zettai.FastNetIkMod), "FastNetIkMod", "0.9", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class FastNetIkMod : MelonMod
    {
        private static MelonPreferences_Entry<bool> netIk;
      //  private static MelonPreferences_Entry<bool> netIkTest;
        private static MelonPreferences_Entry<float> netIkThumbsSplay;
        private static MelonPreferences_Entry<float> netIkIndexSplay;
        private static MelonPreferences_Entry<float> netIkMiddleSplay;
        private static MelonPreferences_Entry<float> netIkRingSplay;
        private static MelonPreferences_Entry<float> netIkLittleSplay;
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            netIk = category.CreateEntry("FastNetIK", true, "Fast NetIK enable");
     //       netIkTest = category.CreateEntry("netIkTest", true, "Fast NetIK test");

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
      //      netIkTest.OnValueChanged += NetIkTest_OnValueChanged;
            Setup.Init(); 
     //       Update.Test = netIkTest.Value;
        }

     //   private void NetIkTest_OnValueChanged(bool arg1, bool arg2) => Update.Test = arg2;

        private void NetIkSplay_OnValueChanged(float arg1, float arg2)
        {
            var thumb = netIkThumbsSplay.Value = Mathf.Clamp(netIkThumbsSplay.Value, -1f, 1f);
            var index = netIkIndexSplay.Value = Mathf.Clamp(netIkIndexSplay.Value, -1f, 1f);
            var middle = netIkMiddleSplay.Value = Mathf.Clamp(netIkMiddleSplay.Value, -1f, 1f);
            var ring = netIkRingSplay.Value = Mathf.Clamp(netIkRingSplay.Value, -1f, 1f);
            var little = netIkLittleSplay.Value = Mathf.Clamp(netIkLittleSplay.Value, -1f, 1f);
            Update.UpdateFingerSpread(thumb, index, middle, ring, little);
        }
              
        public override void OnLateUpdate()
        {
            if (!netIk.Value)
                return;
            Update.StartJobs();
        }
        [HarmonyPatch(typeof(DbJobsColliderUpdate), nameof(DbJobsColliderUpdate.Update))]
        class OnUpdateEnd
        {
            static void Postfix()
            {
                if (!netIk.Value)
                    return;
                Update.StartProcessing();
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
                Update.RemovePlayer(__instance);
            }
        }
        [HarmonyPatch(typeof(PlayerAvatarMovementData), nameof(PlayerAvatarMovementData.WriteDataToAnimatorLerped))]
        class NetIkPatch
        {
            static bool Prefix(PlayerAvatarMovementData __instance, Animator animator, Vector3 relativeHipRotation, bool isBlocked, bool isBlockedAlt, PlayerAvatarMovementData previousData, float progress)
            {
                if (!netIk.Value || animator == null || previousData == null)
                    return true;
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    return false;
                return false;
            }
        }
    }
}
