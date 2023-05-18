using ABI_RC.Core.Player;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Jobs;

namespace Zettai
{
    class ReadNetworkData
    {

        private static readonly List<IkDataPair> dataCache = new List<IkDataPair>();
        private static JobHandle DeserializeHandle;
        private static JobHandle ClearDoneDataHandle;
        private static bool started = false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float SwapFloat(uint value)
        {
            uint intValue = *&value;
            intValue = ((intValue & 0x000000ff) << 24) | ((intValue & 0x0000ff00) << 8) | ((intValue & 0x00ff0000) >> 8) | ((intValue & 0xff000000) >> 24);
            return *(float*)&intValue;
        }
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatSwap
        {
            [FieldOffset(0)] private byte b0;
            [FieldOffset(1)] private byte b1;
            [FieldOffset(2)] private byte b2;
            [FieldOffset(3)] private byte b3;
            [FieldOffset(0)] public uint uintData;
            [FieldOffset(0)] public float floatData;
            public FloatSwap(uint value) : this()
            {
                uintData = value;
                //var a0 = b3;
                //var a1 = b2;
                var a2 = b1;
                var a3 = b0;
                b0 = b3;//a3;
                b1 = b2;//a1;
                b2 = a2;
                b3 = a3;
            }
            public static implicit operator FloatSwap(uint value) => new FloatSwap(value);
            public static implicit operator float(FloatSwap value) => value.floatData;
        }
        [StructLayout(LayoutKind.Explicit)]
        private struct ShortSwap
        {
            [FieldOffset(0)] private byte b0;
            [FieldOffset(1)] private byte b1;
            [FieldOffset(0)] public short shortData;
            public ShortSwap(short value) : this()
            {
                shortData = value;
                var a0 = b1;
                var a1 = b0;
                b0 = a0;
                b1 = a1;
            }
            public static implicit operator ShortSwap(short value) => new ShortSwap(value);
            public static implicit operator short(ShortSwap value) => value.shortData;
        }
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct PlayerAvatarMovementDataInputNoFinger
        {
            public static readonly int size = Marshal.SizeOf(typeof(PlayerAvatarMovementDataInputNoFinger)); //290;
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
            [FieldOffset(288)] public byte IndexUseIndividualFingers;
            [FieldOffset(289)] public byte CameraEnabled;
            public void CopyToClass(PlayerAvatarMovementData data)
            {
                data.DeviceType = (PlayerAvatarMovementData.UsingDeviceType)((ShortSwap)DeviceType).shortData;
                data.RootPosition.x = SwapFloat(RootPositionX);
                data.RootPosition.y = SwapFloat(RootPositionY);
                data.RootPosition.z = SwapFloat(RootPositionZ);
                data.RootRotation.x = SwapFloat(RootRotationX);
                data.RootRotation.y = SwapFloat(RootRotationY);
                data.RootRotation.z = SwapFloat(RootRotationZ);
                data.BodyPosition.x = SwapFloat(BodyPositionX);
                data.BodyPosition.y = SwapFloat(BodyPositionY);
                data.BodyPosition.z = SwapFloat(BodyPositionZ);
                data.BodyRotation.x = SwapFloat(BodyRotationX);
                data.BodyRotation.y = SwapFloat(BodyRotationY);
                data.BodyRotation.z = SwapFloat(BodyRotationZ);
                data.RelativeHipRotation.x = SwapFloat(RelativeHipRotationX);
                data.RelativeHipRotation.y = SwapFloat(RelativeHipRotationY);
                data.RelativeHipRotation.z = SwapFloat(RelativeHipRotationZ);
                data.AnimatorMovementX = SwapFloat(AnimatorMovementX);
                data.AnimatorMovementY = SwapFloat(AnimatorMovementY);
                data.AnimatorGrounded = AnimatorGrounded == 1;
                data.AnimatorEmote = SwapFloat(AnimatorEmote);
                data.AnimatorCancelEmote = AnimatorCancelEmote == 1;
                data.AnimatorGestureLeft = SwapFloat(AnimatorGestureLeft);
                data.AnimatorGestureRight = SwapFloat(AnimatorGestureRight);
                data.AnimatorToggle = SwapFloat(AnimatorToggle);
                data.AnimatorSitting = AnimatorSitting == 1;
                data.AnimatorCrouching = AnimatorCrouching == 1;
                data.AnimatorFlying = AnimatorFlying == 1;
                data.AnimatorProne = AnimatorProne == 1;
                data.SpineFrontBack = SwapFloat(SpineFrontBack);
                data.SpineLeftRight = SwapFloat(SpineLeftRight);
                data.SpineTwistLeftRight = SwapFloat(SpineTwistLeftRight);
                data.ChestFrontBack = SwapFloat(ChestFrontBack);
                data.ChestLeftRight = SwapFloat(ChestLeftRight);
                data.ChestTwistLeftRight = SwapFloat(ChestTwistLeftRight);
                data.UpperChestFrontBack = SwapFloat(UpperChestFrontBack);
                data.UpperChestLeftRight = SwapFloat(UpperChestLeftRight);
                data.UpperChestTwistLeftRight = SwapFloat(UpperChestTwistLeftRight);
                data.NeckNodDownUp = SwapFloat(NeckNodDownUp);
                data.NeckTiltLeftRight = SwapFloat(NeckTiltLeftRight);
                data.NeckTurnLeftRight = SwapFloat(NeckTurnLeftRight);
                data.HeadNodDownUp = SwapFloat(HeadNodDownUp);
                data.HeadTiltLeftRight = SwapFloat(HeadTiltLeftRight);
                data.HeadTurnLeftRight = SwapFloat(HeadTurnLeftRight);
                data.LeftUpperLegFrontBack = SwapFloat(LeftUpperLegFrontBack);
                data.LeftUpperLegInOut = SwapFloat(LeftUpperLegInOut);
                data.LeftUpperLegTwistInOut = SwapFloat(LeftUpperLegTwistInOut);
                data.LeftLowerLegStretch = SwapFloat(LeftLowerLegStretch);
                data.LeftLowerLegTwistInOut = SwapFloat(LeftLowerLegTwistInOut);
                data.LeftFootUpDown = SwapFloat(LeftFootUpDown);
                data.LeftFootTwistInOut = SwapFloat(LeftFootTwistInOut);
                data.LeftToesUpDown = SwapFloat(LeftToesUpDown);
                data.RightUpperLegFrontBack = SwapFloat(RightUpperLegFrontBack);
                data.RightUpperLegInOut = SwapFloat(RightUpperLegInOut);
                data.RightUpperLegTwistInOut = SwapFloat(RightUpperLegTwistInOut);
                data.RightLowerLegStretch = SwapFloat(RightLowerLegStretch);
                data.RightLowerLegTwistInOut = SwapFloat(RightLowerLegTwistInOut);
                data.RightFootUpDown = SwapFloat(RightFootUpDown);
                data.RightFootTwistInOut = SwapFloat(RightFootTwistInOut);
                data.RightToesUpDown = SwapFloat(RightToesUpDown);
                data.LeftShoulderDownUp = SwapFloat(LeftShoulderDownUp);
                data.LeftShoulderFrontBack = SwapFloat(LeftShoulderFrontBack);
                data.LeftArmDownUp = SwapFloat(LeftArmDownUp);
                data.LeftArmFrontBack = SwapFloat(LeftArmFrontBack);
                data.LeftArmTwistInOut = SwapFloat(LeftArmTwistInOut);
                data.LeftForearmStretch = SwapFloat(LeftForearmStretch);
                data.LeftForearmTwistInOut = SwapFloat(LeftForearmTwistInOut);
                data.LeftHandDownUp = SwapFloat(LeftHandDownUp);
                data.LeftHandInOut = SwapFloat(LeftHandInOut);
                data.RightShoulderDownUp = SwapFloat(RightShoulderDownUp);
                data.RightShoulderFrontBack = SwapFloat(RightShoulderFrontBack);
                data.RightArmDownUp = SwapFloat(RightArmDownUp);
                data.RightArmFrontBack = SwapFloat(RightArmFrontBack);
                data.RightArmTwistInOut = SwapFloat(RightArmTwistInOut);
                data.RightForearmStretch = SwapFloat(RightForearmStretch);
                data.RightForearmTwistInOut = SwapFloat(RightForearmTwistInOut);
                data.RightHandDownUp = SwapFloat(RightHandDownUp);
                data.RightHandInOut = SwapFloat(RightHandInOut);
                data.IndexUseIndividualFingers = false;
                data.CameraEnabled = CameraEnabled == 1;
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct PlayerAvatarMovementDataInputFingerOnly
        {
            public static readonly int size = Marshal.SizeOf(typeof(PlayerAvatarMovementDataInputFingerOnly));

            [FieldOffset(00)] public uint LeftThumbCurl;
            [FieldOffset(04)] public uint LeftThumbSpread;
            [FieldOffset(08)] public uint LeftIndexCurl;
            [FieldOffset(12)] public uint LeftIndexSpread;
            [FieldOffset(16)] public uint LeftMiddleCurl;
            [FieldOffset(20)] public uint LeftMiddleSpread;
            [FieldOffset(24)] public uint LeftRingCurl;
            [FieldOffset(28)] public uint LeftRingSpread;
            [FieldOffset(32)] public uint LeftPinkyCurl;
            [FieldOffset(36)] public uint LeftPinkySpread;
            [FieldOffset(40)] public uint RightThumbCurl;
            [FieldOffset(44)] public uint RightThumbSpread;
            [FieldOffset(48)] public uint RightIndexCurl;
            [FieldOffset(52)] public uint RightIndexSpread;
            [FieldOffset(56)] public uint RightMiddleCurl;
            [FieldOffset(60)] public uint RightMiddleSpread;
            [FieldOffset(64)] public uint RightRingCurl;
            [FieldOffset(68)] public uint RightRingSpread;
            [FieldOffset(72)] public uint RightPinkyCurl;
            [FieldOffset(76)] public uint RightPinkySpread;
            [FieldOffset(80)] public byte CameraEnabled;
            public void CopyToClass(PlayerAvatarMovementData data)
            {
                data.IndexUseIndividualFingers = true;
                data.LeftThumbCurl = SwapFloat(LeftThumbCurl);
                data.LeftThumbSpread = SwapFloat(LeftThumbSpread);
                data.LeftIndexCurl = SwapFloat(LeftIndexCurl);
                data.LeftIndexSpread = SwapFloat(LeftIndexSpread);
                data.LeftMiddleCurl = SwapFloat(LeftMiddleCurl);
                data.LeftMiddleSpread = SwapFloat(LeftMiddleSpread);
                data.LeftRingCurl = SwapFloat(LeftRingCurl);
                data.LeftRingSpread = SwapFloat(LeftRingSpread);
                data.LeftPinkyCurl = SwapFloat(LeftPinkyCurl);
                data.LeftPinkySpread = SwapFloat(LeftPinkySpread);
                data.RightThumbCurl = SwapFloat(RightThumbCurl);
                data.RightThumbSpread = SwapFloat(RightThumbSpread);
                data.RightIndexCurl = SwapFloat(RightIndexCurl);
                data.RightIndexSpread = SwapFloat(RightIndexSpread);
                data.RightMiddleCurl = SwapFloat(RightMiddleCurl);
                data.RightMiddleSpread = SwapFloat(RightMiddleSpread);
                data.RightRingCurl = SwapFloat(RightRingCurl);
                data.RightRingSpread = SwapFloat(RightRingSpread);
                data.RightPinkyCurl = SwapFloat(RightPinkyCurl);
                data.RightPinkySpread = SwapFloat(RightPinkySpread);
                data.CameraEnabled = CameraEnabled == 1;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct BytesToFloatInt
        {
            [FieldOffset(0)] public int intValue;
            [FieldOffset(0)] public byte b0;
            [FieldOffset(1)] public byte b1;
            [FieldOffset(2)] public byte b2;
            [FieldOffset(3)] public byte b3;
            public BytesToFloatInt(byte[] data, int start)
            {
                intValue = 0;
                b3 = data[start];
                b2 = data[start + 1];
                b1 = data[start + 2];
                b0 = data[start + 3];
            }
        }

        private static unsafe bool GetPlayerPuppetMaster(byte[] buffer, out PuppetMaster pm)
        {
            pm = null;
            var uuid = stackalloc char[36];
            for (int i = 0; i < 36; i++)
            {
                uuid[i] = (char)buffer[7 + (2 * i)];
            }
            foreach (var player in CVRPlayerManager.Instance.NetworkPlayers)
            {
                if (player == null || !CharsEqualString(uuid, player.Uuid))
                    continue;
                pm = player.PuppetMaster;
                return true;
            }
            return false;
        }
        private static unsafe bool CharsEqualString(char* uuid, string text)
        {
            if (text == null || text.Length != 36)
                return false;
            for (int i = 0; i < 36; i++)
            {
                if (text[i] != uuid[i])
                    return false;
            }
            return true;
        }
        public struct IkDataPair
        {
            public bool isDone;
            public bool failed;
            public PlayerAvatarMovementData input;
            public DarkRift.DarkRiftReader darkRiftReader;
            public IkDataPair(PlayerAvatarMovementData _input, DarkRift.DarkRiftReader _darkRiftReader)
            {
                isDone = failed = false;
                input = _input;
                darkRiftReader = _darkRiftReader;
            }
        }

        public static void StartProcessing() 
        {
            if (dataCache.Count == 0 || started)
                return;
            started = true;
            var deserializeIkJob = new DeserializeIkJob();
            var clearDoneDataJob = new ClearDoneDataJob();

            DeserializeHandle = deserializeIkJob.Schedule(dataCache.Count, 2);
            ClearDoneDataHandle = clearDoneDataJob.Schedule(DeserializeHandle);
            JobHandle.ScheduleBatchedJobs();
        }
        public static void StopProcessing()
        {
            if (!started)
                return;
            ClearDoneDataHandle.Complete();
            started = false;
        }

        public static void AddData(DarkRift.Message message)
        {
            var reader = message.GetReader();
            var buffer = reader.buffer.Buffer;
            if (GetPlayerPuppetMaster(buffer, out var puppetMaster))
            {
                puppetMaster.CycleData();
                dataCache.Add(new IkDataPair(puppetMaster.PlayerAvatarMovementDataInput, reader));
            }
        }

        public static void ClearAllCachedData()
        {
            for (int i = 0; i < dataCache.Count; i++)
            {
                dataCache[i].darkRiftReader.Dispose();
            }
            dataCache.Clear();
        }

        public struct ClearDoneDataJob : IJob
        {
            public void Execute()
            {
                for (int i = dataCache.Count - 1; i >= 0; i--)
                {
                    if (dataCache[i].isDone)
                    {
                        dataCache[i].darkRiftReader.Dispose();
                        dataCache.RemoveAt(i);
                    }
                }
            }
        }

        public struct DeserializeIkJob : IJobParallelFor
        {
            public void Execute(int index)
            {
                var _data = dataCache[index];
                if (_data.isDone)
                    return;
                _data.failed = Apply(_data.darkRiftReader.buffer.Buffer, _data.input);                
                _data.isDone = true;
                dataCache[index] = _data;
            }
        }
        private static unsafe bool Apply(byte[] buffer, PlayerAvatarMovementData input)
        {
            if (buffer == null || input == null)
                return false;
            var idLength = new BytesToFloatInt(buffer, 3);
            var dataStart = idLength.intValue + 7; // 79
            if (buffer.Length < PlayerAvatarMovementDataInputNoFinger.size + dataStart + 3)
                return false;
            bool fingerDataPresent = buffer[288 + dataStart] != 0;

            fixed (byte* bufferPtr = buffer)
            {
                PlayerAvatarMovementDataInputNoFinger baseData = *(PlayerAvatarMovementDataInputNoFinger*)(bufferPtr + dataStart);
                baseData.CopyToClass(input);
                dataStart += PlayerAvatarMovementDataInputNoFinger.size;
                if (fingerDataPresent)
                {
                    PlayerAvatarMovementDataInputFingerOnly fingers = *(PlayerAvatarMovementDataInputFingerOnly*)(bufferPtr + dataStart);
                    fingers.CopyToClass(input);
                    dataStart += PlayerAvatarMovementDataInputFingerOnly.size;
                }
                if (input.CameraEnabled)
                {
                    uint* floats = (uint*)(bufferPtr + dataStart);
                    input.CameraPosition.x = SwapFloat(floats[0]);
                    input.CameraPosition.y = SwapFloat(floats[1]);
                    input.CameraPosition.z = SwapFloat(floats[2]);
                    input.CameraRotation.x = SwapFloat(floats[3]);
                    input.CameraRotation.y = SwapFloat(floats[4]);
                    input.CameraRotation.z = SwapFloat(floats[5]);
                    dataStart += 24;
                }
                bool eyeTracking = input.EyeTrackingOverride = bufferPtr[dataStart++] != 0;
                if (eyeTracking)
                {
                    uint* floats = (uint*)(bufferPtr + dataStart);
                    input.EyeTrackingPosition.x = SwapFloat(floats[0]);
                    input.EyeTrackingPosition.y = SwapFloat(floats[1]);
                    input.EyeTrackingPosition.z = SwapFloat(floats[2]);
                    dataStart += 12;
                }
                bool eyeBlink = input.EyeBlinkingOverride = bufferPtr[dataStart++] != 0;
                if (eyeBlink)
                {
                    uint* floats = (uint*)(bufferPtr + dataStart);
                    input.EyeTrackingBlinkProgressLeft = SwapFloat(floats[0]);
                    input.EyeTrackingBlinkProgressRight = SwapFloat(floats[1]);
                    dataStart += 24;
                }
                bool faceTracking = input.FaceTrackingEnabled = bufferPtr[dataStart++] != 0;
                if (faceTracking)
                {
                    uint* floats = (uint*)(bufferPtr + dataStart);
                    for (int i = 0; i < 37; i++)
                    {
                        input.FaceTrackingData[i] = SwapFloat(floats[i]);
                    }
                }
            }
            return true;
        }
    }
}
