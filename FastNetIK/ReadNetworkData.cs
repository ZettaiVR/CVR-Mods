using ABI_RC.Core.Player;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Jobs;

namespace Zettai
{
    internal class ReadNetworkData
    {
        private static readonly List<IkDataPair> dataCache = new List<IkDataPair>();

        private static readonly Stack<byte[]> bufferCache = new Stack<byte[]>();
        internal static JobHandle DeserializeHandle;
        internal static bool started = false;
      
        /// <summary>
        /// Swap endianness of a float and makes sure it's value is within reason for numbers at most 8.5 billion
        /// </summary>
        /// <param name="value">The float as an uint</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe float SwapFloat(uint value)
        {
            uint intValue = value;
            intValue = ((intValue & 0x000000ff) << 24) | ((intValue & 0x0000ff00) << 8) | ((intValue & 0x00ff0000) >> 8) | ((intValue & 0xff000000) >> 24);
            var abs = intValue & 0x7FFFFFFF;
            //  NaN or inf or > 8 589 934 080
            if (abs >= 0x50000000)
                return 0f;
            return *(float*)&intValue;
        }

        /// <summary>
        /// Swap endianness of a float with no limits on it
        /// </summary>
        /// <param name="value">The float as an uint</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe float SwapFloatUnlimited(uint value)
        {
            uint intValue = value;
            intValue = ((intValue & 0x000000ff) << 24) | ((intValue & 0x0000ff00) << 8) | ((intValue & 0x00ff0000) >> 8) | ((intValue & 0xff000000) >> 24);
            return *(float*)&intValue;
        }

        /// <summary>
        /// Swap endianness of a float and makes sure it's value is within reason for numbers smaller than 65536
        /// </summary>
        /// <param name="value">The float as an uint</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe float SwapSmallFloat(uint value)
        {
            uint intValue = value;
            intValue = ((intValue & 0x000000ff) << 24) | ((intValue & 0x0000ff00) << 8) | ((intValue & 0x00ff0000) >> 8) | ((intValue & 0xff000000) >> 24);
            var abs = intValue & 0x7FFFFFFF;
            // NaN or inf or > 65 535
            if (abs >= 0x48000000)
                return 0f;
            return *(float*)&intValue;
        }

        /// <summary>
        /// Swap endianness of a short 
        /// </summary>
        /// <param name="value">The uint to swap</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short SwapShort(short value) => (short)(((value & 0x00ff) << 8) | ((value & 0x00ff) >> 8));

        /// <summary>
        /// Swap endianness of an int from a byte array. No bounds check performed.
        /// </summary>
        /// <param name="data">The byte array</param>
        /// <param name="start">start of the int in the array</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SwapInt(byte[] data, int start) => (data[start] << 24) | (data[start + 1] << 16) | (data[start + 2] << 8) | (data[start + 3]);

        private static unsafe bool GetPlayerPuppetMaster(byte[] buffer, out PuppetMaster pm)
        {
            pm = null;
            if (buffer == null)
                return false;
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

        public static void StartProcessing() 
        {
            if (dataCache.Count == 0 || started)
                return;
            started = true;
            var deserializeIkJob = new DeserializeIkJob();

            DeserializeHandle = deserializeIkJob.Schedule(dataCache.Count, 2);
        }
        const int CachedBufferSize = 2048;
        private static byte[] GetBuffer(byte[] buffer)
        {
            if (buffer == null)
                return null;
            int size = buffer.Length;
            byte[] data;
            if (bufferCache.Count == 0 || size > CachedBufferSize)
            {
                data = size > CachedBufferSize ? new byte[size] : new byte[CachedBufferSize];
            //    MelonLoader.MelonLogger.Msg($"new array {size}, cache: {bufferCache.Count}");
            }
            else
            {
                data = bufferCache.Pop();
            }
            System.Array.Copy(buffer, data, buffer.Length);
            return data;
        }
        private static void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length != CachedBufferSize)
                return;
            bufferCache.Push(buffer);      
        }

        public static void AddData(DarkRift.Message message)
        {
            var buffer = message.buffer.Buffer;
            var newBuffer = GetBuffer(buffer);
            if (GetPlayerPuppetMaster(newBuffer, out var puppetMaster))
            {
                puppetMaster.CycleData();
                dataCache.Add(new IkDataPair(puppetMaster, newBuffer));
            }
        }
        public static void CompleteProcessing()
        {
            DeserializeHandle.Complete();
            ClearAllCachedData();
            started = false;
        }
        public static void ClearAllCachedData()
        {
            for (int i = 0; i < dataCache.Count; i++)
            {
                ReturnBuffer(dataCache[i].data);
            }
            dataCache.Clear();
        }

        public struct DeserializeIkJob : IJobParallelFor
        {
            public void Execute(int index)
            {
                var _data = dataCache[index];
                if (_data.isDone)
                    return;
                _data.failed = Apply(_data.data, _data.input);                
                _data.isDone = true;
                dataCache[index] = _data;
            }
        }
        private static unsafe bool Apply(byte[] buffer, PuppetMaster puppetMaster)
        {
            var input = puppetMaster?.PlayerAvatarMovementDataInput;
            if (buffer == null || input == null)
                return false;
            var idLength = SwapInt(buffer, 3);
            var dataStart = idLength + 7; // 79 usually
            if (buffer.Length < PlayerAvatarMovementDataInputBase.size + dataStart + 5) // 5 bools
                return false;

            fixed (byte* bufferPtr = buffer)
            {
                PlayerAvatarMovementDataInputBase baseData = *(PlayerAvatarMovementDataInputBase*)(bufferPtr + dataStart);
                baseData.CopyToClass(input);
                dataStart += PlayerAvatarMovementDataInputBase.size;
                bool fingerDataPresent = bufferPtr[dataStart++] == 1;
                if (fingerDataPresent)
                {
                    PlayerAvatarMovementDataInputFingerOnly fingers = *(PlayerAvatarMovementDataInputFingerOnly*)(bufferPtr + dataStart);
                    fingers.CopyToClass(input);
                    dataStart += PlayerAvatarMovementDataInputFingerOnly.size;
                }
                bool cameraEnabled = input.CameraEnabled = bufferPtr[dataStart++] == 1;
                if (cameraEnabled)
                {
                    CameraData cam = *(CameraData*)(bufferPtr + dataStart);
                    cam.CopyToClass(input);
                    dataStart += CameraData.size;
                }
                bool eyeTracking = input.EyeTrackingOverride = bufferPtr[dataStart++] == 1;
                if (eyeTracking)
                {
                    EyeTrackingPositionData eyePos = *(EyeTrackingPositionData*)(bufferPtr + dataStart);
                    eyePos.CopyToClass(input);
                    dataStart += EyeTrackingPositionData.size;
                }
                bool eyeBlink = input.EyeBlinkingOverride = bufferPtr[dataStart++] == 1;
                if (eyeBlink)
                {
                    EyeTrackingBlinkData eyeBlinkData = *(EyeTrackingBlinkData*)(bufferPtr + dataStart);
                    eyeBlinkData.CopyToClass(input);
                    dataStart += EyeTrackingBlinkData.size;
                }
                var data = bufferPtr[dataStart++];
                bool faceTracking = input.FaceTrackingEnabled = data == 1;
                if (faceTracking)
                {
                    uint* floats = (uint*)(bufferPtr + dataStart);
                    var firstFloat = SwapFloatUnlimited(floats[0]);
                    for (int i = 0; i < input.FaceTrackingData.Length; i++)
                    {
                        input.FaceTrackingData[i] = SwapSmallFloat(floats[i]);
                    }
                }

            }
            return true;
        }

        
    }
}
