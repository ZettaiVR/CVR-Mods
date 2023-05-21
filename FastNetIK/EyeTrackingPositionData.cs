using ABI_RC.Core.Player;
using System.Runtime.InteropServices;

namespace Zettai
{
    [StructLayout(LayoutKind.Explicit)]
    struct EyeTrackingPositionData
    {
        public static readonly int size = Marshal.SizeOf(typeof(EyeTrackingPositionData));

        [FieldOffset(00)] public uint posX;
        [FieldOffset(04)] public uint posY;
        [FieldOffset(08)] public uint posZ;
        public void CopyToClass(PlayerAvatarMovementData data)
        {
            data.EyeTrackingPosition.x = ReadNetworkData.SwapFloat(posX);
            data.EyeTrackingPosition.y = ReadNetworkData.SwapFloat(posY);
            data.EyeTrackingPosition.z = ReadNetworkData.SwapFloat(posZ);
        }
    }
}
