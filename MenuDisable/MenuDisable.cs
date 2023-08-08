using ABI_RC.Core.InteractionSystem;
using cohtml.Net;
using HarmonyLib;
using MelonLoader;
using System;
using System.Diagnostics;

[assembly: MelonInfo(typeof(Zettai.MenuDisable), "MenuDisable", "0.9", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class MenuDisable:MelonMod
    {
        private static readonly MelonPreferences_Category category = MelonPreferences.CreateCategory("Zettai");
        private static readonly MelonPreferences_Entry<bool> MenuDisablePref = category.CreateEntry("MenuDisable", true, "MenuDisable");
        private static readonly Stopwatch sw = new Stopwatch();

        [HarmonyPatch(typeof(View), nameof(View.Advance))]
        class ViewAdvancePatch
        {
            private static bool init = true;
            public static bool Prefix(View __instance)
            {
                if (init)
                {
                    sw.Start();
                    // give the menus 5 seconds to initialize on start
                    if (sw.ElapsedMilliseconds > 5000)
                    {
                        init = false;
                        sw.Stop();
                    }
                    return true;
                }
                if (!MenuDisablePref.Value)
                    return true;
                if (CVR_MenuManager.Instance.quickMenu?.View == __instance)
                {
                    if (!CVR_MenuManager.Instance._quickMenuOpen)
                    {
                        return false;
                    }
                }
                else if (ViewManager.Instance.gameMenuView.View == __instance && !ViewManager.Instance.isGameMenuOpen())
                {
                    return false;
                }
                return true;
            }
        }
    }
}
