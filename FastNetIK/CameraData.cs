using ABI_RC.Core.Player;
using System.Runtime.InteropServices;

namespace Zettai
{
    [StructLayout(LayoutKind.Explicit)]
    struct CameraData
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
            data.CameraPosition.x = ReadNetworkData.SwapFloat(posX);
            data.CameraPosition.y = ReadNetworkData.SwapFloat(posY);
            data.CameraPosition.z = ReadNetworkData.SwapFloat(posZ);
            data.CameraRotation.x = ReadNetworkData.SwapSmallFloat(rotX);
            data.CameraRotation.y = ReadNetworkData.SwapSmallFloat(rotY);
            data.CameraRotation.z = ReadNetworkData.SwapSmallFloat(rotZ);
        }
    }

}
