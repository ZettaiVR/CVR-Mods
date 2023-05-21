using ABI_RC.Core.Player;
using System.Runtime.InteropServices;

namespace Zettai
{
    [StructLayout(LayoutKind.Explicit)]
    struct EyeTrackingBlinkData
    {
        public static readonly int size = Marshal.SizeOf(typeof(EyeTrackingBlinkData));

        [FieldOffset(00)] public uint left;
        [FieldOffset(04)] public uint right;
        public void CopyToClass(PlayerAvatarMovementData data)
        {
            data.EyeTrackingBlinkProgressLeft = ReadNetworkData.SwapFloat(left);
            data.EyeTrackingBlinkProgressRight = ReadNetworkData.SwapFloat(right);
        }
    }
}
