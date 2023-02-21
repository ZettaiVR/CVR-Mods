using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public struct BoneElement
    {
        public float twistValue;
        public float middleMultiplier;
        public bool4 dofExists;
        public int3 muscleIds;
        public float3 min;
        public float3 max;
        public float3 center;
        public float3 sign;
        public Quaternion preQ;
        public Quaternion preQInv;
        public Quaternion postQ;
        public Quaternion postQInv;
        public HumanBodyBones humanBodyBoneId;
    }
}
