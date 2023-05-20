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
      
        /// <summary>
        /// Swap endianness of a float and makes sure it's value is within reason for numbers at most 8.5 billion
        /// </summary>
        /// <param name="value">The float as an uint</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float SwapFloat(uint value)
        {
            uint intValue = value;
            intValue = ((intValue & 0x000000ff) << 24) | ((intValue & 0x0000ff00) << 8) | ((intValue & 0x00ff0000) >> 8) | ((intValue & 0xff000000) >> 24);
            var abs = intValue & 0x7FFFFFFF;
            //         NaN or inf            > 1                     > 8 589 934 080
            if (abs >= 0x7F800000 || ((abs & 0x40000000) > 0 && (abs & 0x30000000) > 0))
            {
                return 0f;
            }
            return *(float*)&intValue;
        }

        /// <summary>
        /// Swap endianness of a float and makes sure it's value is within reason for numbers smaller than 65536
        /// </summary>
        /// <param name="value">The float as an uint</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float SwapSmallFloat(uint value)
        {
            uint intValue = value;
            intValue = ((intValue & 0x000000ff) << 24) | ((intValue & 0x0000ff00) << 8) | ((intValue & 0x00ff0000) >> 8) | ((intValue & 0xff000000) >> 24);
            var abs = intValue & 0x7FFFFFFF;
            //         NaN or inf            > 1                     > 65535
            if (abs >= 0x7F800000 || ((abs & 0x40000000) > 0 && (abs & 0x38000000) > 0))
            {
                return 0f;
            }
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
            public void CopyToClass(PlayerAvatarMovementData data)
            {
                data.DeviceType = (PlayerAvatarMovementData.UsingDeviceType)((ShortSwap)DeviceType).shortData;
                data.RootPosition.x = SwapFloat(RootPositionX);
                data.RootPosition.y = SwapFloat(RootPositionY);
                data.RootPosition.z = SwapFloat(RootPositionZ);
                data.RootRotation.x = SwapSmallFloat(RootRotationX);
                data.RootRotation.y = SwapSmallFloat(RootRotationY);
                data.RootRotation.z = SwapSmallFloat(RootRotationZ);
                data.BodyPosition.x = SwapFloat(BodyPositionX);
                data.BodyPosition.y = SwapFloat(BodyPositionY);
                data.BodyPosition.z = SwapFloat(BodyPositionZ);
                data.BodyRotation.x = SwapSmallFloat(BodyRotationX);
                data.BodyRotation.y = SwapSmallFloat(BodyRotationY);
                data.BodyRotation.z = SwapSmallFloat(BodyRotationZ);
                data.RelativeHipRotation.x = SwapSmallFloat(RelativeHipRotationX);
                data.RelativeHipRotation.y = SwapSmallFloat(RelativeHipRotationY);
                data.RelativeHipRotation.z = SwapSmallFloat(RelativeHipRotationZ);
                data.AnimatorMovementX = SwapSmallFloat(AnimatorMovementX);
                data.AnimatorMovementY = SwapSmallFloat(AnimatorMovementY);
                data.AnimatorGrounded = AnimatorGrounded == 1;
                data.AnimatorEmote = SwapSmallFloat(AnimatorEmote);
                data.AnimatorCancelEmote = AnimatorCancelEmote == 1;
                data.AnimatorGestureLeft = SwapSmallFloat(AnimatorGestureLeft);
                data.AnimatorGestureRight = SwapSmallFloat(AnimatorGestureRight);
                data.AnimatorToggle = SwapSmallFloat(AnimatorToggle);
                data.AnimatorSitting = AnimatorSitting == 1;
                data.AnimatorCrouching = AnimatorCrouching == 1;
                data.AnimatorFlying = AnimatorFlying == 1;
                data.AnimatorProne = AnimatorProne == 1;
                data.SpineFrontBack = SwapSmallFloat(SpineFrontBack);
                data.SpineLeftRight = SwapSmallFloat(SpineLeftRight);
                data.SpineTwistLeftRight = SwapSmallFloat(SpineTwistLeftRight);
                data.ChestFrontBack = SwapSmallFloat(ChestFrontBack);
                data.ChestLeftRight = SwapSmallFloat(ChestLeftRight);
                data.ChestTwistLeftRight = SwapSmallFloat(ChestTwistLeftRight);
                data.UpperChestFrontBack = SwapSmallFloat(UpperChestFrontBack);
                data.UpperChestLeftRight = SwapSmallFloat(UpperChestLeftRight);
                data.UpperChestTwistLeftRight = SwapSmallFloat(UpperChestTwistLeftRight);
                data.NeckNodDownUp = SwapSmallFloat(NeckNodDownUp);
                data.NeckTiltLeftRight = SwapSmallFloat(NeckTiltLeftRight);
                data.NeckTurnLeftRight = SwapSmallFloat(NeckTurnLeftRight);
                data.HeadNodDownUp = SwapSmallFloat(HeadNodDownUp);
                data.HeadTiltLeftRight = SwapSmallFloat(HeadTiltLeftRight);
                data.HeadTurnLeftRight = SwapSmallFloat(HeadTurnLeftRight);
                data.LeftUpperLegFrontBack = SwapSmallFloat(LeftUpperLegFrontBack);
                data.LeftUpperLegInOut = SwapSmallFloat(LeftUpperLegInOut);
                data.LeftUpperLegTwistInOut = SwapSmallFloat(LeftUpperLegTwistInOut);
                data.LeftLowerLegStretch = SwapSmallFloat(LeftLowerLegStretch);
                data.LeftLowerLegTwistInOut = SwapSmallFloat(LeftLowerLegTwistInOut);
                data.LeftFootUpDown = SwapSmallFloat(LeftFootUpDown);
                data.LeftFootTwistInOut = SwapSmallFloat(LeftFootTwistInOut);
                data.LeftToesUpDown = SwapSmallFloat(LeftToesUpDown);
                data.RightUpperLegFrontBack = SwapSmallFloat(RightUpperLegFrontBack);
                data.RightUpperLegInOut = SwapSmallFloat(RightUpperLegInOut);
                data.RightUpperLegTwistInOut = SwapSmallFloat(RightUpperLegTwistInOut);
                data.RightLowerLegStretch = SwapSmallFloat(RightLowerLegStretch);
                data.RightLowerLegTwistInOut = SwapSmallFloat(RightLowerLegTwistInOut);
                data.RightFootUpDown = SwapSmallFloat(RightFootUpDown);
                data.RightFootTwistInOut = SwapSmallFloat(RightFootTwistInOut);
                data.RightToesUpDown = SwapSmallFloat(RightToesUpDown);
                data.LeftShoulderDownUp = SwapSmallFloat(LeftShoulderDownUp);
                data.LeftShoulderFrontBack = SwapSmallFloat(LeftShoulderFrontBack);
                data.LeftArmDownUp = SwapSmallFloat(LeftArmDownUp);
                data.LeftArmFrontBack = SwapSmallFloat(LeftArmFrontBack);
                data.LeftArmTwistInOut = SwapSmallFloat(LeftArmTwistInOut);
                data.LeftForearmStretch = SwapSmallFloat(LeftForearmStretch);
                data.LeftForearmTwistInOut = SwapSmallFloat(LeftForearmTwistInOut);
                data.LeftHandDownUp = SwapSmallFloat(LeftHandDownUp);
                data.LeftHandInOut = SwapSmallFloat(LeftHandInOut);
                data.RightShoulderDownUp = SwapSmallFloat(RightShoulderDownUp);
                data.RightShoulderFrontBack = SwapSmallFloat(RightShoulderFrontBack);
                data.RightArmDownUp = SwapSmallFloat(RightArmDownUp);
                data.RightArmFrontBack = SwapSmallFloat(RightArmFrontBack);
                data.RightArmTwistInOut = SwapSmallFloat(RightArmTwistInOut);
                data.RightForearmStretch = SwapSmallFloat(RightForearmStretch);
                data.RightForearmTwistInOut = SwapSmallFloat(RightForearmTwistInOut);
                data.RightHandDownUp = SwapSmallFloat(RightHandDownUp);
                data.RightHandInOut = SwapSmallFloat(RightHandInOut);
                data.IndexUseIndividualFingers = false;
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
            public void CopyToClass(PlayerAvatarMovementData data)
            {
                data.IndexUseIndividualFingers = true;
                data.LeftThumbCurl = SwapSmallFloat(LeftThumbCurl);
                data.LeftThumbSpread = SwapSmallFloat(LeftThumbSpread);
                data.LeftIndexCurl = SwapSmallFloat(LeftIndexCurl);
                data.LeftIndexSpread = SwapSmallFloat(LeftIndexSpread);
                data.LeftMiddleCurl = SwapSmallFloat(LeftMiddleCurl);
                data.LeftMiddleSpread = SwapSmallFloat(LeftMiddleSpread);
                data.LeftRingCurl = SwapSmallFloat(LeftRingCurl);
                data.LeftRingSpread = SwapSmallFloat(LeftRingSpread);
                data.LeftPinkyCurl = SwapSmallFloat(LeftPinkyCurl);
                data.LeftPinkySpread = SwapSmallFloat(LeftPinkySpread);
                data.RightThumbCurl = SwapSmallFloat(RightThumbCurl);
                data.RightThumbSpread = SwapSmallFloat(RightThumbSpread);
                data.RightIndexCurl = SwapSmallFloat(RightIndexCurl);
                data.RightIndexSpread = SwapSmallFloat(RightIndexSpread);
                data.RightMiddleCurl = SwapSmallFloat(RightMiddleCurl);
                data.RightMiddleSpread = SwapSmallFloat(RightMiddleSpread);
                data.RightRingCurl = SwapSmallFloat(RightRingCurl);
                data.RightRingSpread = SwapSmallFloat(RightRingSpread);
                data.RightPinkyCurl = SwapSmallFloat(RightPinkyCurl);
                data.RightPinkySpread = SwapSmallFloat(RightPinkySpread);
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct CameraData
        {
            public static readonly int size = Marshal.SizeOf(typeof(CameraData));

            [FieldOffset(00)] public uint posX;
            [FieldOffset(04)] public uint posY;
            [FieldOffset(08)] public uint posZ;
            [FieldOffset(12)] public uint rotX;
            [FieldOffset(16)] public uint rotY;
            [FieldOffset(20)] public uint rotZ;
            public void CopyToClass(PlayerAvatarMovementData data)
            {
                data.CameraPosition.x = SwapFloat(posX);
                data.CameraPosition.y = SwapFloat(posY);
                data.CameraPosition.z = SwapFloat(posZ);
                data.CameraRotation.x = SwapSmallFloat(rotX);
                data.CameraRotation.y = SwapSmallFloat(rotY);
                data.CameraRotation.z = SwapSmallFloat(rotZ);
            }
        }
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct EyeTrackingPositionData
        {
            public static readonly int size = Marshal.SizeOf(typeof(EyeTrackingPositionData));

            [FieldOffset(00)] public uint posX;
            [FieldOffset(04)] public uint posY;
            [FieldOffset(08)] public uint posZ;
            public void CopyToClass(PlayerAvatarMovementData data)
            {
                data.EyeTrackingPosition.x = SwapFloat(posX);
                data.EyeTrackingPosition.y = SwapFloat(posY);
                data.EyeTrackingPosition.z = SwapFloat(posZ);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct BytesToInt
        {
            [FieldOffset(0)] public int intValue;
            [FieldOffset(0)] public byte b0;
            [FieldOffset(1)] public byte b1;
            [FieldOffset(2)] public byte b2;
            [FieldOffset(3)] public byte b3;
            public BytesToInt(byte[] data, int start)
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
            var idLength = new BytesToInt(buffer, 3);
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
                bool cameraEnabled = input.CameraEnabled = bufferPtr[dataStart++] != 0;
                if (cameraEnabled)
                {
                    CameraData cam = *(CameraData*)(bufferPtr + dataStart);
                    cam.CopyToClass(input);
                    dataStart += CameraData.size;
                }
                bool eyeTracking = input.EyeTrackingOverride = bufferPtr[dataStart++] != 0;
                if (eyeTracking)
                {
                    EyeTrackingPositionData eyePos = *(EyeTrackingPositionData*)(bufferPtr + dataStart);
                    eyePos.CopyToClass(input);
                    dataStart += EyeTrackingPositionData.size;
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
                        input.FaceTrackingData[i] = SwapSmallFloat(floats[i]);
                    }
                }
            }
            return true;
        }
    }
}
