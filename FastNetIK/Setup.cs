using ABI_RC.Core.Player;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Zettai
{
    class Setup
    {
        private const int MaxThreads = 8;
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
        internal static void Init(int count)
        {
            count = Mathf.Clamp(count, 1, MaxThreads);
            StartNewThreads(count);
            Update.ArrayInit();
        }
        
        public static bool GetPlayer(PuppetMaster player, ref NetIkData value) => Update.players.TryGetValue(player, out value);
        public static bool GetPlayer(Animator animator, ref NetIkData value)
        {
            if (Update.puppetMasters.TryGetValue(animator, out var pm)) { 
                return Update.players.TryGetValue(pm, out value);}
            return false;
        }
        public static void SetThreadCount(int count) 
        {
            var currentCount = threads.Count;
            if (currentCount == count)
                return;
            if (currentCount > count) 
            {
                int removeCount = currentCount - count;
                for (int i = threads.Count - 1; i >= 0; i--)
                {
                    threads[i].Abort();
                    threads.RemoveAt(i);
                    removeCount--;
                    if (removeCount == 0)
                        break;
                }
                Update.threadCount = count;
                return;
            }
            int addCount = count - currentCount;
            StartNewThreads(addCount);
            Update.threadCount = count;
        }
        private static void StartNewThreads(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                if (threads.Count + i > MaxThreads)
                    break;
                string threadName = $"[NetIK] {threads.Count + i}";
                StartNewThread(threadName, Update.NetIkProcess);
            }
            Update.threadCount = count;
        }
        private static void StartNewThread(string name, ThreadStart threadStart)
        {
            var thread = new Thread(threadStart)
            {
                IsBackground = true,
                Name = name
            };
            threads.Add(thread);
            thread.Priority = System.Threading.ThreadPriority.BelowNormal;
            thread.Start();
        }
    }
}
