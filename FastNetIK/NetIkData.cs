using ABI_RC.Core.Player;
using UnityEngine;

namespace Zettai
{
    public class NetIkData
    {
        public const int BoneCount = (int)HumanBodyBones.LastBone;

        public bool fingers;
        public Avatar avatar;
        public Animator animator;
        public PuppetMaster puppetMaster;
        public BoneElement[] boneElements = new BoneElement[BoneCount];
        public PlayerAvatarMovementData dataCurr;
        public PlayerAvatarMovementData dataPrev;
        public Transform[] rotTransforms = new Transform[BoneCount];
        public Quaternion[] rotations1 = new Quaternion[BoneCount];
        public Quaternion[] rotations2 = new Quaternion[BoneCount];
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
