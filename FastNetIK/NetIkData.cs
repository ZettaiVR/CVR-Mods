using ABI_RC.Core.Player;
using Unity.Mathematics;
using UnityEngine;

namespace Zettai
{
    public class NetIkData
    {
        private static readonly int BoneCount = (int)HumanBodyBones.LastBone;
        public Avatar avatar;
        public Animator animator;
        public PuppetMaster puppetMaster;
        public BoneElement[] boneElements = new BoneElement[BoneCount];
        public float upperArmTwist;
        public float lowerArmTwist;
        public float upperLegTwist;
        public float lowerLegTwist;
        public float armStretch;
        public float legStretch;
        public float feetSpacing;
        public PlayerAvatarMovementData dataCurr;
        public PlayerAvatarMovementData dataPrev;
        public Transform[] rotTransforms = new Transform[BoneCount];
        public quaternion[] rotations1 = new quaternion[BoneCount];
        public quaternion[] rotations2 = new quaternion[BoneCount];
        public TransformInfoInit[] transformInfos = new TransformInfoInit[BoneCount];
        public Transform root;
        public Transform hips;

        public Quaternion rootRotInterpolated;
        public Quaternion rootRot1;
        public Quaternion rootRot2;
        public Quaternion hipsRotInterpolated;
        public Quaternion hipsRot1;
        public Quaternion hipsRot2;
        public Vector3 rootPosInterpolated;
        public Vector3 rootPos1;
        public Vector3 rootPos2;
        public Vector3 hipsPosInterpolated;
        public Vector3 hipsPos1;
        public Vector3 hipsPos2;
        public float updateCurr;
        public float updatePrev;
    }
}
