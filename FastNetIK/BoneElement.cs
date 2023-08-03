using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public struct BoneElement
    {
        internal static BoneElement empty = new BoneElement();
        public Quaternion preQ;
        public Quaternion preQInv;
        public Quaternion postQ;
        public Quaternion postQInv;
        public Quaternion twistQ;
        public int3 muscleIds;
        public float3 min;
        public float3 max;
        public float3 center;
        public float twistValue;
        public bool4 dofExists;
        public HumanBodyBones humanBodyBoneId;
        public readonly bool BoneExists => dofExists.w;
    }
}
