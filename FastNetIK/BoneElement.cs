using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public struct BoneElement
    {
        public Vector3 min;
        public Vector3 minAbs;
        public Vector3 max;
       //public Vector3 center;
        public Transform transform;
        public bool4 dofExists;
        public int3 muscleIds;
        public Vector3 sign;
        public Quaternion preQ;
        public Quaternion preQInv;
        public Quaternion postQ;
        public Quaternion postQInv;
        public HumanBodyBones humanBodyBoneId;
        public float twistValue;
        public int mixIndex;
    }
}
