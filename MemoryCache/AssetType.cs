namespace Zettai
{
    public enum AssetType
    {
        Avatar = 1,
        Scene = 2,
        Prop = 4,
        HiddenAvatar = 8,
        Other = 16,
        Unknown = 128
    }
    public static class AssetTypeExt
    {
        public static AssetType Value(this ABI_RC.Core.IO.DownloadTask.ObjectType type) 
        {
            switch (type)
            {
                case ABI_RC.Core.IO.DownloadTask.ObjectType.Avatar:
                    return AssetType.Avatar;
                case ABI_RC.Core.IO.DownloadTask.ObjectType.World:
                    return AssetType.Scene;
                case ABI_RC.Core.IO.DownloadTask.ObjectType.Prop:
                    return AssetType.Prop;
                default:
                    return AssetType.Unknown;
            }
        }
        public static AssetType ToAssetType(this ABI_RC.Core.IO.DownloadTask.ObjectType type) => Value(type);
    }
}
