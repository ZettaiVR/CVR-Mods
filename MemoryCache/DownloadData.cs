using ABI_RC.Core.IO;
using ABI_RC.Core.Networking.API.Responses;
using System.Net;

namespace Zettai
{
    internal class DownloadData
    {
        public bool IsDone { get; set; }
        public bool DownloadDone { get; set; }
        public bool ReadyToInstantiate { get; set; }
        public bool Failed => FileReadFailed || DecryptFailed || VerifyFailed;
        public byte[] rawData;
        public byte[] decryptedData;
        public string filePath;
        public string calculatedHash;
        public readonly bool isLocal;
        public readonly bool joinOnComplete;
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
        private readonly ABI_RC.Core.InteractionSystem.CVRLoadingAvatarController loadingController;

        private readonly WebClient client = new WebClient();
        private int percentageComplete = 0;
        private int previousPercentageComplete = 0;

        public void DownloadFile()
        {
            client.DownloadProgressChanged += Client_DownloadProgressChanged;
            client.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCompleted);
            client.DownloadDataAsync(new System.Uri(assetUrl));
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            var p = e.ProgressPercentage;
            p /= 5;
            p *= 5;
            percentageComplete = p;
        }
        internal void UpdateLoadingAvatar()
        {
            if (!loadingController)
            {
                return;
            }
            if (percentageComplete == previousPercentageComplete)
                return;
            previousPercentageComplete = percentageComplete;
            if (!FileCache.PercentageText.TryGetValue(percentageComplete, out var text))
                text = $"{percentageComplete} %";
            loadingController.textMesh.text = text;
            loadingController.textMesh.ForceMeshUpdate(forceTextReparsing: true);
        }
        private void DownloadFileCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                DownloadDone = true;
                rawData = e.Result;
            }
            client?.Dispose();
        }

        public DownloadData(string AssetId, DownloadTask.ObjectType Type, string AssetUrl, string FileId, long FileSize, string FileKey, string FileHash,
            bool JoinOnComplete, UgcTagsData TagsData, string Target, string FilePath, ABI_RC.Core.InteractionSystem.CVRLoadingAvatarController loadingAvatarController)
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
            loadingController = loadingAvatarController;
            IsDone = false;
            Verified = false;
            DownloadDone = false;
            ReadyToInstantiate = false;
        }

        internal void Destroy()
        {
            client?.Dispose();
        }
    }
}