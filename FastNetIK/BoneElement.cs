using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public struct BoneElement
    {
        public float3 min;
        public float3 minAbs;
        public float3 max;
        public float3 center;
        public Transform transform;
        public bool4 dofExists;
        public int3 muscleIds;
        public float3 sign;
        public Quaternion preQ;
        public Quaternion preQInv;
        public Quaternion postQ;
        public Quaternion postQInv;
        public HumanBodyBones humanBodyBoneId;
        public float twistValue;
        public int mixIndex;
    }
}
