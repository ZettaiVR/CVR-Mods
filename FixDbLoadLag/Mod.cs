using ABI_RC.Core.Player;
using HarmonyLib;
using MelonLoader;
using System.Reflection;
using Zettai;

[assembly: MelonInfo(typeof(FixDbLoadLag), "FixDbLoadLag", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]

namespace Zettai
{
    public class FixDbLoadLag : MelonMod
	{
		private static MelonPreferences_Entry<bool> enableDbLagPatch;
        private static readonly MethodInfo __UpdateComponents = typeof(CVRDynamicBoneManager).GetMethod(nameof(CVRDynamicBoneManager.UpdateComponents), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly System.Action UpdateComponentsDelegate = (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), __UpdateComponents);
        public override void OnApplicationStart()
		{
			var category = MelonPreferences.CreateCategory("Zettai");
			enableDbLagPatch = category.CreateEntry("enableDbLagPatch", true, "Enable Dynamic bone loading lag patch");
		}
        [HarmonyPatch(typeof(CVRDynamicBoneManager), nameof(CVRDynamicBoneManager.UpdateComponents))]
        class UpdateDbComponentsPatch
        {
            public static bool letItRun = false;
            static bool update = false;
            static bool Prefix()
            {
                if (!enableDbLagPatch.Value)
                    return true;
                if (!letItRun)
                {
                    update = true;
                    return false;
                }
                bool value = update;
                update = false;
                return value;
            }
        }
        [HarmonyPatch(typeof(DbJobsColliderUpdate), nameof(DbJobsColliderUpdate.Update))]
        class UpdateDbComponentsRun
        {
            static bool Prefix()
            {
                if (!enableDbLagPatch.Value)
                    return true;
                UpdateDbComponentsPatch.letItRun = true;
                UpdateComponentsDelegate();
                UpdateDbComponentsPatch.letItRun = false;
                return true;
            }
        }

    }
}
