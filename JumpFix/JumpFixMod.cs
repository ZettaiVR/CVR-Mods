using ABI.CCK.Components;
using ABI_RC.Systems.MovementSystem;
using HarmonyLib;
using MelonLoader;
using Zettai;

[assembly: MelonInfo(typeof(JumpFixMod), "JumpFixMod", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class JumpFixMod : MelonMod
    {
        private static MelonPreferences_Entry<bool> enableJumpMod;
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            enableJumpMod = category.CreateEntry("enableJumpFixMod", true, "Enable JumpFix mod");
            enableJumpMod.OnValueChanged += EnableJumpMod_OnValueChanged;
        }

        private void EnableJumpMod_OnValueChanged(bool arg1, bool arg2) => SetGravity();

        private static void SetGravity()
        {
            if (enableJumpMod.Value && MovementSystem.Instance.gravity == 18f)
                MovementSystem.Instance.gravity = 9.81f;
        }

        [HarmonyPatch(typeof(CVRWorld))]
        class AvatarStartPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(CVRWorld.Start))]
            static void WorldStartPostfix(CVRWorld __instance)
            {
                if (enableJumpMod.Value && __instance.gravity == 18f)
                    __instance.gravity = 9.81f;
                SetGravity();
                return;
            }
        }

    }
}
