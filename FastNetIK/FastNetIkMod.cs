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
        private static MelonPreferences_Entry<int> netIkThreads;
        private static bool processingEnded = false;
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            netIk = category.CreateEntry("FastNetIK", true, "Fast NetIK enable");
            netIkThreads = category.CreateEntry("netIkThreads", 2, "NetIK thread count");
            Setup.Init(netIkThreads.Value);
        }
        public override void OnApplicationQuit()
        {
            Update.AbortAllThreads();
        }
        [HarmonyPatch(typeof(DbJobsColliderUpdate), nameof(DbJobsColliderUpdate.Update))]
        class OnUpdateEnd
        {
            static void Postfix()
            {
                if (!netIk.Value)
                    return;

                Update.StartProcessing();
                processingEnded = false;
            }
        }
        [HarmonyPatch(typeof(PuppetMaster), nameof(PuppetMaster.AvatarInstantiated))]
        class PuppetMasterAvatarInstantiated
        {
            static void Postfix(PuppetMaster __instance)
            {
                if (!string.IsNullOrEmpty(__instance._playerDescriptor.ownerId))
                    Setup.AddPlayer(__instance);
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
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.LateUpdate))]
        class PlayerSetupLateUpdate
        {
            static void Prefix()
            {
                if (!netIk.Value)
                    return;

                Update.StartJobs();
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
                if (!processingEnded)
                {
                    Update.EndProcessing();
                    processingEnded = true;
                }


                NetIkData netIkData = null;
                if (Setup.GetPlayer(animator, ref netIkData))
                {
                    var hipsRotInterpolated = netIkData.hipsRotInterpolated;
                    if (isBlocked && !isBlockedAlt)
                    {
                        var rot = hipsRotInterpolated.eulerAngles;
                        rot -= __instance.RelativeHipRotation - relativeHipRotation;
                        hipsRotInterpolated = Quaternion.Euler(rot);
                    }
                    // order important!
                    netIkData.root.SetPositionAndRotation(netIkData.rootPosInterpolated, netIkData.rootRotInterpolated);
                    netIkData.hips.SetPositionAndRotation(netIkData.hipsPosInterpolated, hipsRotInterpolated);
                    return false;
                }
                return true;
            }
        }
    }
}
