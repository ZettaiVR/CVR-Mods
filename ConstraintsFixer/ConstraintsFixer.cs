using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using Zettai;

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
            var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            int preLateUpdateIndex = -1;
            int postLateUpdateIndex = -1;
            for (int i = 0; i < currentPlayerLoop.subSystemList.Length; i++)
            {
                if (currentPlayerLoop.subSystemList[i].type == typeof(PreLateUpdate))
                {
                    preLateUpdateIndex = i;
                    continue;
                }
                if (currentPlayerLoop.subSystemList[i].type == typeof(PostLateUpdate))
                {
                    postLateUpdateIndex = i;
                }
            }
            if (preLateUpdateIndex == -1 || postLateUpdateIndex == -1)
                return;

            var preLateUpdate = currentPlayerLoop.subSystemList[preLateUpdateIndex];
            var postLateUpdate = currentPlayerLoop.subSystemList[postLateUpdateIndex];
            var preLateUpdateList = new List<PlayerLoopSystem>(preLateUpdate.subSystemList);
            var postLateUpdateList = new List<PlayerLoopSystem>(postLateUpdate.subSystemList);

            SwapSystem(preLateUpdateList, postLateUpdateList, typeof(PreLateUpdate.EndGraphicsJobsAfterScriptUpdate));
            SwapSystem(preLateUpdateList, postLateUpdateList, typeof(PreLateUpdate.ParticleSystemBeginUpdateAll));
            SwapSystem(preLateUpdateList, postLateUpdateList, typeof(PreLateUpdate.ConstraintManagerUpdate));

            preLateUpdate.subSystemList = preLateUpdateList.ToArray();
            postLateUpdate.subSystemList = postLateUpdateList.ToArray();
            currentPlayerLoop.subSystemList[preLateUpdateIndex] = preLateUpdate;
            currentPlayerLoop.subSystemList[postLateUpdateIndex] = postLateUpdate;
            PlayerLoop.SetPlayerLoop(currentPlayerLoop);
        }

        private static void SwapSystem(List<PlayerLoopSystem> preLateUpdateList, List<PlayerLoopSystem> postLateUpdateList, Type type)
        {
            int index = preLateUpdateList.FindIndex((PlayerLoopSystem x) => x.type == type);
            if (index >= 0)
            {
                var system = preLateUpdateList[index];
                postLateUpdateList.Insert(1, system);
                preLateUpdateList.Remove(system);
            }
        }
    }
}