using ABI_RC.Core.Player;
using System.Runtime.InteropServices;

namespace Zettai
{
    [StructLayout(LayoutKind.Explicit)]
    struct PlayerAvatarMovementDataInputFingerOnly
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
            data.LeftThumbCurl = ReadNetworkData.SwapSmallFloat(LeftThumbCurl);
            data.LeftThumbSpread = ReadNetworkData.SwapSmallFloat(LeftThumbSpread);
            data.LeftIndexCurl = ReadNetworkData.SwapSmallFloat(LeftIndexCurl);
            data.LeftIndexSpread = ReadNetworkData.SwapSmallFloat(LeftIndexSpread);
            data.LeftMiddleCurl = ReadNetworkData.SwapSmallFloat(LeftMiddleCurl);
            data.LeftMiddleSpread = ReadNetworkData.SwapSmallFloat(LeftMiddleSpread);
            data.LeftRingCurl = ReadNetworkData.SwapSmallFloat(LeftRingCurl);
            data.LeftRingSpread = ReadNetworkData.SwapSmallFloat(LeftRingSpread);
            data.LeftPinkyCurl = ReadNetworkData.SwapSmallFloat(LeftPinkyCurl);
            data.LeftPinkySpread = ReadNetworkData.SwapSmallFloat(LeftPinkySpread);
            data.RightThumbCurl = ReadNetworkData.SwapSmallFloat(RightThumbCurl);
            data.RightThumbSpread = ReadNetworkData.SwapSmallFloat(RightThumbSpread);
            data.RightIndexCurl = ReadNetworkData.SwapSmallFloat(RightIndexCurl);
            data.RightIndexSpread = ReadNetworkData.SwapSmallFloat(RightIndexSpread);
            data.RightMiddleCurl = ReadNetworkData.SwapSmallFloat(RightMiddleCurl);
            data.RightMiddleSpread = ReadNetworkData.SwapSmallFloat(RightMiddleSpread);
            data.RightRingCurl = ReadNetworkData.SwapSmallFloat(RightRingCurl);
            data.RightRingSpread = ReadNetworkData.SwapSmallFloat(RightRingSpread);
            data.RightPinkyCurl = ReadNetworkData.SwapSmallFloat(RightPinkyCurl);
            data.RightPinkySpread = ReadNetworkData.SwapSmallFloat(RightPinkySpread);
        }
    }
}