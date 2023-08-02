using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public struct BoneElement
    {
        public Quaternion preQ;
        public Quaternion preQInv;
        public Quaternion postQ;
        public Quaternion postQInv;
        public Quaternion twistQ;
        public float3 min;
        public float3 max;
        public float3 center;
        public int3 muscleIds;
        public float twistValue;
        public bool4 dofExists;
        public HumanBodyBones humanBodyBoneId;
        public readonly bool BoneExists => dofExists.w;
    }
}
