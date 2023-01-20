using ABI_RC.Core.IO;
using ABI_RC.Core.Networking.API.Responses;
using System.Threading;

namespace Zettai
{
    internal class DownloadData
    {
        public bool IsDone { get; set; }
        public bool DownloadDone { get; set; }
        public bool ReadyToInstantiate { get; set; }
        public bool Failed => FileReadFailed || DecryptFailed || VerifyFailed;
        public int PercentageComplete { get; internal set; }
        public byte[] rawData;
        public byte[] decryptedData;
        public string filePath;
        public string calculatedHash;
        public readonly bool isLocal;
        public readonly bool joinOnComplete;
        public readonly byte[] rawKey;
        public readonly ulong fileSize;
        public readonly string assetId;
        public readonly string assetUrl;
        public readonly string fileId;
        public readonly string fileKey;
        public readonly string fileHash;
        public readonly string target;
        public readonly Tags tags;
        public readonly DownloadTask.ObjectType type;

        public volatile bool FileWriteDone = false;
        public volatile bool FileReadDone = false;
        public volatile bool HashDone = false;
        public volatile bool DecryptDone = false;
        public volatile bool VerifyDone = false;

        public volatile bool FileWriteFailed = false;
        public volatile bool FileReadFailed = false;
        public volatile bool HashFailed = false;
        public volatile bool DecryptFailed = false;
        public volatile bool VerifyFailed = false;
        public volatile bool Verified = false;

        public volatile Status status = Status.None;

        public CancellationToken cancellationToken = new CancellationToken();
        internal long prevDownloaded = 0;
        internal int rateLimit = 0;

        public DownloadData(string AssetId, byte[] RawData, byte[] FileKey)
        {
            assetId = AssetId;
            rawData = RawData;
            rawKey = FileKey;
            fileSize = (ulong)RawData?.Length;
        }

        public DownloadData(string AssetId, DownloadTask.ObjectType Type, string AssetUrl, string FileId, long FileSize, string FileKey, string FileHash,
            bool JoinOnComplete, UgcTagsData TagsData, string Target, string FilePath, int downloadLimit)
        {
            joinOnComplete = JoinOnComplete;
            assetId = AssetId;
            assetUrl = AssetUrl;
            filePath = FilePath;
            fileId = FileId;
            fileSize = (ulong)FileSize;
            fileKey = FileKey;
            fileHash = FileHash;
            target = Target;
            type = Type;
            tags = new Tags(TagsData);
            isLocal = MemoryCache.IsLocal(target);
            IsDone = false;
            Verified = false;
            DownloadDone = false;
            ReadyToInstantiate = false;
            rateLimit = downloadLimit; // Bytes/sec
        }
        public enum Status 
        {
            None,
            NotStarted,
            DownloadQueued,
            Downloading,
            LoadingFromFileQueued,
            LoadingFromFile,
            HashingQueued,
            Hashing,
            DecryptingQueued,
            Decrypting,
            BundleVerifierQueued,
            BundleVerifier,
            LoadingBundleQueued,
            LoadingBundle,
            Done,
            Error
        }
    }
}