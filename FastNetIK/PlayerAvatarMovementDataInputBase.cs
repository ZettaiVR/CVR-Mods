﻿using ABI_RC.Core.Player;
using System.Runtime.InteropServices;

namespace Zettai
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    struct PlayerAvatarMovementDataInputBase
    {
        public static readonly int size = Marshal.SizeOf(typeof(PlayerAvatarMovementDataInputBase)); // 288;
        [FieldOffset(0)] public short DeviceType;
        [FieldOffset(2)] public uint RootPositionX;
        [FieldOffset(6)] public uint RootPositionY;
        [FieldOffset(10)] public uint RootPositionZ;
        [FieldOffset(14)] public uint RootRotationX;
        [FieldOffset(18)] public uint RootRotationY;
        [FieldOffset(22)] public uint RootRotationZ;
        [FieldOffset(26)] public uint BodyPositionX;
        [FieldOffset(30)] public uint BodyPositionY;
        [FieldOffset(34)] public uint BodyPositionZ;
        [FieldOffset(38)] public uint BodyRotationX;
        [FieldOffset(42)] public uint BodyRotationY;
        [FieldOffset(46)] public uint BodyRotationZ;
        [FieldOffset(50)] public uint RelativeHipRotationX;
        [FieldOffset(54)] public uint RelativeHipRotationY;
        [FieldOffset(58)] public uint RelativeHipRotationZ;
        [FieldOffset(62)] public uint AnimatorMovementX;
        [FieldOffset(66)] public uint AnimatorMovementY;
        [FieldOffset(70)] public byte AnimatorGrounded;
        [FieldOffset(71)] public uint AnimatorEmote;
        [FieldOffset(75)] public byte AnimatorCancelEmote;
        [FieldOffset(76)] public uint AnimatorGestureLeft;
        [FieldOffset(80)] public uint AnimatorGestureRight;
        [FieldOffset(84)] public uint AnimatorToggle;
        [FieldOffset(88)] public byte AnimatorSitting;
        [FieldOffset(89)] public byte AnimatorCrouching;
        [FieldOffset(90)] public byte AnimatorFlying;
        [FieldOffset(91)] public byte AnimatorProne;
        [FieldOffset(92)] public uint SpineFrontBack;
        [FieldOffset(96)] public uint SpineLeftRight;
        [FieldOffset(100)] public uint SpineTwistLeftRight;
        [FieldOffset(104)] public uint ChestFrontBack;
        [FieldOffset(108)] public uint ChestLeftRight;
        [FieldOffset(112)] public uint ChestTwistLeftRight;
        [FieldOffset(116)] public uint UpperChestFrontBack;
        [FieldOffset(120)] public uint UpperChestLeftRight;
        [FieldOffset(124)] public uint UpperChestTwistLeftRight;
        [FieldOffset(128)] public uint NeckNodDownUp;
        [FieldOffset(132)] public uint NeckTiltLeftRight;
        [FieldOffset(136)] public uint NeckTurnLeftRight;
        [FieldOffset(140)] public uint HeadNodDownUp;
        [FieldOffset(144)] public uint HeadTiltLeftRight;
        [FieldOffset(148)] public uint HeadTurnLeftRight;
        [FieldOffset(152)] public uint LeftUpperLegFrontBack;
        [FieldOffset(156)] public uint LeftUpperLegInOut;
        [FieldOffset(160)] public uint LeftUpperLegTwistInOut;
        [FieldOffset(164)] public uint LeftLowerLegStretch;
        [FieldOffset(168)] public uint LeftLowerLegTwistInOut;
        [FieldOffset(172)] public uint LeftFootUpDown;
        [FieldOffset(176)] public uint LeftFootTwistInOut;
        [FieldOffset(180)] public uint LeftToesUpDown;
        [FieldOffset(184)] public uint RightUpperLegFrontBack;
        [FieldOffset(188)] public uint RightUpperLegInOut;
        [FieldOffset(192)] public uint RightUpperLegTwistInOut;
        [FieldOffset(196)] public uint RightLowerLegStretch;
        [FieldOffset(200)] public uint RightLowerLegTwistInOut;
        [FieldOffset(204)] public uint RightFootUpDown;
        [FieldOffset(208)] public uint RightFootTwistInOut;
        [FieldOffset(212)] public uint RightToesUpDown;
        [FieldOffset(216)] public uint LeftShoulderDownUp;
        [FieldOffset(220)] public uint LeftShoulderFrontBack;
        [FieldOffset(224)] public uint LeftArmDownUp;
        [FieldOffset(228)] public uint LeftArmFrontBack;
        [FieldOffset(232)] public uint LeftArmTwistInOut;
        [FieldOffset(236)] public uint LeftForearmStretch;
        [FieldOffset(240)] public uint LeftForearmTwistInOut;
        [FieldOffset(244)] public uint LeftHandDownUp;
        [FieldOffset(248)] public uint LeftHandInOut;
        [FieldOffset(252)] public uint RightShoulderDownUp;
        [FieldOffset(256)] public uint RightShoulderFrontBack;
        [FieldOffset(260)] public uint RightArmDownUp;
        [FieldOffset(264)] public uint RightArmFrontBack;
        [FieldOffset(268)] public uint RightArmTwistInOut;
        [FieldOffset(272)] public uint RightForearmStretch;
        [FieldOffset(276)] public uint RightForearmTwistInOut;
        [FieldOffset(280)] public uint RightHandDownUp;
        [FieldOffset(284)] public uint RightHandInOut;
        public void CopyToClass(PlayerAvatarMovementData data)
        {
            data.DeviceType = (PlayerAvatarMovementData.UsingDeviceType)ReadNetworkData.SwapShort(DeviceType);
            data.RootPosition.x = ReadNetworkData.SwapFloat(RootPositionX);
            data.RootPosition.y = ReadNetworkData.SwapFloat(RootPositionY);
            data.RootPosition.z = ReadNetworkData.SwapFloat(RootPositionZ);
            data.RootRotation.x = ReadNetworkData.SwapSmallFloat(RootRotationX);
            data.RootRotation.y = ReadNetworkData.SwapSmallFloat(RootRotationY);
            data.RootRotation.z = ReadNetworkData.SwapSmallFloat(RootRotationZ);
            data.BodyPosition.x = ReadNetworkData.SwapFloat(BodyPositionX);
            data.BodyPosition.y = ReadNetworkData.SwapFloat(BodyPositionY);
            data.BodyPosition.z = ReadNetworkData.SwapFloat(BodyPositionZ);
            data.BodyRotation.x = ReadNetworkData.SwapSmallFloat(BodyRotationX);
            data.BodyRotation.y = ReadNetworkData.SwapSmallFloat(BodyRotationY);
            data.BodyRotation.z = ReadNetworkData.SwapSmallFloat(BodyRotationZ);
            data.RelativeHipRotation.x = ReadNetworkData.SwapSmallFloat(RelativeHipRotationX);
            data.RelativeHipRotation.y = ReadNetworkData.SwapSmallFloat(RelativeHipRotationY);
            data.RelativeHipRotation.z = ReadNetworkData.SwapSmallFloat(RelativeHipRotationZ);
            data.AnimatorMovementX = ReadNetworkData.SwapSmallFloat(AnimatorMovementX);
            data.AnimatorMovementY = ReadNetworkData.SwapSmallFloat(AnimatorMovementY);
            data.AnimatorGrounded = AnimatorGrounded == 1;
            data.AnimatorEmote = ReadNetworkData.SwapSmallFloat(AnimatorEmote);
            data.AnimatorCancelEmote = AnimatorCancelEmote == 1;
            data.AnimatorGestureLeft = ReadNetworkData.SwapSmallFloat(AnimatorGestureLeft);
            data.AnimatorGestureRight = ReadNetworkData.SwapSmallFloat(AnimatorGestureRight);
            data.AnimatorToggle = ReadNetworkData.SwapSmallFloat(AnimatorToggle);
            data.AnimatorSitting = AnimatorSitting == 1;
            data.AnimatorCrouching = AnimatorCrouching == 1;
            data.AnimatorFlying = AnimatorFlying == 1;
            data.AnimatorProne = AnimatorProne == 1;
            data.SpineFrontBack = ReadNetworkData.SwapSmallFloat(SpineFrontBack);
            data.SpineLeftRight = ReadNetworkData.SwapSmallFloat(SpineLeftRight);
            data.SpineTwistLeftRight = ReadNetworkData.SwapSmallFloat(SpineTwistLeftRight);
            data.ChestFrontBack = ReadNetworkData.SwapSmallFloat(ChestFrontBack);
            data.ChestLeftRight = ReadNetworkData.SwapSmallFloat(ChestLeftRight);
            data.ChestTwistLeftRight = ReadNetworkData.SwapSmallFloat(ChestTwistLeftRight);
            data.UpperChestFrontBack = ReadNetworkData.SwapSmallFloat(UpperChestFrontBack);
            data.UpperChestLeftRight = ReadNetworkData.SwapSmallFloat(UpperChestLeftRight);
            data.UpperChestTwistLeftRight = ReadNetworkData.SwapSmallFloat(UpperChestTwistLeftRight);
            data.NeckNodDownUp = ReadNetworkData.SwapSmallFloat(NeckNodDownUp);
            data.NeckTiltLeftRight = ReadNetworkData.SwapSmallFloat(NeckTiltLeftRight);
            data.NeckTurnLeftRight = ReadNetworkData.SwapSmallFloat(NeckTurnLeftRight);
            data.HeadNodDownUp = ReadNetworkData.SwapSmallFloat(HeadNodDownUp);
            data.HeadTiltLeftRight = ReadNetworkData.SwapSmallFloat(HeadTiltLeftRight);
            data.HeadTurnLeftRight = ReadNetworkData.SwapSmallFloat(HeadTurnLeftRight);
            data.LeftUpperLegFrontBack = ReadNetworkData.SwapSmallFloat(LeftUpperLegFrontBack);
            data.LeftUpperLegInOut = ReadNetworkData.SwapSmallFloat(LeftUpperLegInOut);
            data.LeftUpperLegTwistInOut = ReadNetworkData.SwapSmallFloat(LeftUpperLegTwistInOut);
            data.LeftLowerLegStretch = ReadNetworkData.SwapSmallFloat(LeftLowerLegStretch);
            data.LeftLowerLegTwistInOut = ReadNetworkData.SwapSmallFloat(LeftLowerLegTwistInOut);
            data.LeftFootUpDown = ReadNetworkData.SwapSmallFloat(LeftFootUpDown);
            data.LeftFootTwistInOut = ReadNetworkData.SwapSmallFloat(LeftFootTwistInOut);
            data.LeftToesUpDown = ReadNetworkData.SwapSmallFloat(LeftToesUpDown);
            data.RightUpperLegFrontBack = ReadNetworkData.SwapSmallFloat(RightUpperLegFrontBack);
            data.RightUpperLegInOut = ReadNetworkData.SwapSmallFloat(RightUpperLegInOut);
            data.RightUpperLegTwistInOut = ReadNetworkData.SwapSmallFloat(RightUpperLegTwistInOut);
            data.RightLowerLegStretch = ReadNetworkData.SwapSmallFloat(RightLowerLegStretch);
            data.RightLowerLegTwistInOut = ReadNetworkData.SwapSmallFloat(RightLowerLegTwistInOut);
            data.RightFootUpDown = ReadNetworkData.SwapSmallFloat(RightFootUpDown);
            data.RightFootTwistInOut = ReadNetworkData.SwapSmallFloat(RightFootTwistInOut);
            data.RightToesUpDown = ReadNetworkData.SwapSmallFloat(RightToesUpDown);
            data.LeftShoulderDownUp = ReadNetworkData.SwapSmallFloat(LeftShoulderDownUp);
            data.LeftShoulderFrontBack = ReadNetworkData.SwapSmallFloat(LeftShoulderFrontBack);
            data.LeftArmDownUp = ReadNetworkData.SwapSmallFloat(LeftArmDownUp);
            data.LeftArmFrontBack = ReadNetworkData.SwapSmallFloat(LeftArmFrontBack);
            data.LeftArmTwistInOut = ReadNetworkData.SwapSmallFloat(LeftArmTwistInOut);
            data.LeftForearmStretch = ReadNetworkData.SwapSmallFloat(LeftForearmStretch);
            data.LeftForearmTwistInOut = ReadNetworkData.SwapSmallFloat(LeftForearmTwistInOut);
            data.LeftHandDownUp = ReadNetworkData.SwapSmallFloat(LeftHandDownUp);
            data.LeftHandInOut = ReadNetworkData.SwapSmallFloat(LeftHandInOut);
            data.RightShoulderDownUp = ReadNetworkData.SwapSmallFloat(RightShoulderDownUp);
            data.RightShoulderFrontBack = ReadNetworkData.SwapSmallFloat(RightShoulderFrontBack);
            data.RightArmDownUp = ReadNetworkData.SwapSmallFloat(RightArmDownUp);
            data.RightArmFrontBack = ReadNetworkData.SwapSmallFloat(RightArmFrontBack);
            data.RightArmTwistInOut = ReadNetworkData.SwapSmallFloat(RightArmTwistInOut);
            data.RightForearmStretch = ReadNetworkData.SwapSmallFloat(RightForearmStretch);
            data.RightForearmTwistInOut = ReadNetworkData.SwapSmallFloat(RightForearmTwistInOut);
            data.RightHandDownUp = ReadNetworkData.SwapSmallFloat(RightHandDownUp);
            data.RightHandInOut = ReadNetworkData.SwapSmallFloat(RightHandInOut);
            data.IndexUseIndividualFingers = false;
        }
    }
}
