using ABI_RC.Core.Player;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Zettai
{
    class Setup
    {
        private static readonly List<Thread> threads = new List<Thread>();
        public static void AddPlayer(PuppetMaster player)
        {
            var data = new NetIkData();
            data.puppetMaster = player;
            var animator = data.animator = player?._animator;
            if (!animator || !animator.isHuman || !animator.avatar)
                return;
            Update.puppetMasters[animator] = player;
            data.avatar = animator.avatar;
            for (int i = 0; i < data.rotTransforms.Length; i++)
            {
                data.rotTransforms[i] = animator.GetBoneTransform((HumanBodyBones)i);
            }
            data.hips = data.rotTransforms[(int)HumanBodyBones.Hips];
            data.root = animator.transform;
            PoseHandling.CalibrateMuscles(data.animator, data.boneElements, data.transformInfos);
            Update.players.Add(player, data);
            Update.allPlayers.Add(player);
        }
        internal static void Init(int count = 2)
        {
            count = Mathf.Clamp(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount / 4, 1, 8);
            StartNewThreads("NetIK", Update.NetIkProcess, count);
            Update.threadCount = count;
        }
        
        public static bool GetPlayer(PuppetMaster player, ref NetIkData value) => Update.players.TryGetValue(player, out value);
        public static bool GetPlayer(Animator animator, ref NetIkData value)
        {
            if (Update.puppetMasters.TryGetValue(animator, out var pm)) { 
                return Update.players.TryGetValue(pm, out value);}
            return false;
        }

        public static void StartNewThreads(string name, ThreadStart threadStart, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var thread = new Thread(threadStart)
                {
                    IsBackground = true,
                    Name = count == 1 ? $"[NetIK] {name}" : $"[NetIK] {name} {i + 1}"
                };
                threads.Add(thread);
                thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                thread.Start();
            }
        }
        
    }
}
