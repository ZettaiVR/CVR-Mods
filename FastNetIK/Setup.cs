using ABI_RC.Core.Player;
using UnityEngine;

namespace Zettai
{
    class Setup
    {
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
            Update.players[player] = data;
            Update.allPlayers.Add(player);
        }
        internal static void Init()
        {
            Update.ArrayInit();
        }
        
        public static bool GetPlayer(PuppetMaster player, ref NetIkData value) => Update.players.TryGetValue(player, out value);
        public static bool GetPlayer(Animator animator, ref NetIkData value)
        {
            if (Update.puppetMasters.TryGetValue(animator, out var pm))  
                return Update.players.TryGetValue(pm, out value);
            return false;
        }
    }
}
