using HarmonyLib;
using MelonLoader;
using RootMotion.FinalIK;
using System;
using UnityEngine;

[assembly: MelonInfo(typeof(Zettai.FixLegCalibration), "FixLegCalibration", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]

namespace Zettai
{
    public class FixLegCalibration : MelonMod
	{
		[HarmonyPatch(typeof(VRIKCalibrator), nameof(VRIKCalibrator.CalibrateLeg), new Type[] { typeof(VRIKCalibrator.Settings),
		typeof(Transform), typeof(IKSolverVR.Leg), typeof(Transform),typeof(Vector3), typeof(bool), typeof(float)})]
		class CalibrateLegPatch
		{
            private const string LeftFootTarget = "Left Foot Target";
            private const string RightFootTarget = "Right Foot Target";
            private const string LeftLegBendGoal = "Left Leg Bend Goal";
            private const string RightLegBendGoal = "Right Leg Bend Goal";

            static bool Prefix(VRIKCalibrator.Settings settings, Transform tracker, IKSolverVR.Leg leg, Transform lastBone,
				Vector3 rootForward, bool isLeft, float offset = 0f)
			{
				foreach (Transform obj in tracker.transform)
					UnityEngine.Object.Destroy(obj.gameObject);

				var footTargetName = isLeft ? LeftFootTarget : RightFootTarget;
				var bendTargetName = isLeft ? LeftLegBendGoal : RightLegBendGoal;
				var target = leg.target;
				if (!target)
					target = new GameObject(footTargetName).transform;
				target.parent = lastBone;
				target.localPosition = Vector3.zero;
				target.localRotation = Quaternion.identity;
				target.parent = tracker;
				leg.target = target;
				leg.positionWeight = 1f;
				leg.rotationWeight = 1f;
				var bendTarget = leg.bendGoal;
				if (!bendTarget)
					bendTarget = new GameObject(bendTargetName).transform;
				bendTarget.parent = target;
				rootForward *= 0.2f;
				rootForward.y += 1f;
				rootForward *= leg.thigh.length;
				bendTarget.position = lastBone.position + rootForward;
				leg.bendGoal = bendTarget;
				leg.bendGoalWeight = 1f;
				return false;
			}
		}
	}
}
