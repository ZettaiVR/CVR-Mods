using MelonLoader;
using System.Collections.Generic;
using Zettai;
using System.Linq;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

[assembly: MelonInfo(typeof(ConstraintsFixer), "Constraints Fixer", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class ConstraintsFixer : MelonMod
    {
        public override void OnInitializeMelon()
        {
            PlayerLoopTweaker();
        }
        public static void PlayerLoopTweaker()
        {
            PlayerLoopSystem currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            int num = -1;
            int num2 = -1;
            for (int i = 0; i < currentPlayerLoop.subSystemList.Length; i++)
            {
                if (currentPlayerLoop.subSystemList[i].type == typeof(PreLateUpdate))
                {
                    num = i;
                }
                if (currentPlayerLoop.subSystemList[i].type == typeof(PostLateUpdate))
                {
                    num2 = i;
                }
            }
            PlayerLoopSystem playerLoopSystem = currentPlayerLoop.subSystemList[num];
            PlayerLoopSystem playerLoopSystem2 = currentPlayerLoop.subSystemList[num2];
            List<PlayerLoopSystem> list = new List<PlayerLoopSystem>(playerLoopSystem.subSystemList);
            List<PlayerLoopSystem> list2 = new List<PlayerLoopSystem>(playerLoopSystem2.subSystemList);
            PlayerLoopSystem playerLoopSystem3 = list.FirstOrDefault((PlayerLoopSystem x) => x.type == typeof(PreLateUpdate.ConstraintManagerUpdate));
            PlayerLoopSystem playerLoopSystem4 = list.FirstOrDefault((PlayerLoopSystem x) => x.type == typeof(PreLateUpdate.ParticleSystemBeginUpdateAll));
            PlayerLoopSystem playerLoopSystem5 = list.FirstOrDefault((PlayerLoopSystem x) => x.type == typeof(PreLateUpdate.EndGraphicsJobsAfterScriptUpdate));
            list2.Insert(0, playerLoopSystem3);
            list2.Insert(1, playerLoopSystem4);
            list2.Insert(2, playerLoopSystem5);
            list.Remove(playerLoopSystem3);
            list.Remove(playerLoopSystem4);
            list.Remove(playerLoopSystem5);
            playerLoopSystem.subSystemList = list.ToArray();
            playerLoopSystem2.subSystemList = list2.ToArray();
            currentPlayerLoop.subSystemList[num] = playerLoopSystem;
            currentPlayerLoop.subSystemList[num2] = playerLoopSystem2;
            PlayerLoop.SetPlayerLoop(currentPlayerLoop);
        }
    }
}
