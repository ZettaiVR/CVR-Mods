using ABI_RC.Core.Player;

namespace Zettai
{
    internal class LoadTask
    {
        public readonly DownloadData DownloadData;
        private readonly ABI_RC.Core.InteractionSystem.CVRLoadingAvatarController loadingAvatar;
        public DownloadData.Status Status => DownloadData == null ? DownloadData.Status.None : DownloadData.status;
        private DownloadData.Status prevStatus = DownloadData.Status.None;
        private int previousPercentageComplete = 0;
        public LoadTask(DownloadData downloadData, CVRPlayerEntity player, bool isLocal)
        {
            DownloadData = downloadData;
            if (!isLocal && player != null)
            {
                if (player.LoadingAvatar == null)
                    ABI_RC.Core.InteractionSystem.CVRLoadingAvatarController.Initialize(player);
                loadingAvatar = player.LoadingAvatar;
                loadingAvatar.textMesh.autoSizeTextContainer = true;
                UpdateLoadingAvatar();
            }

        }
        public void UpdateLoadingAvatar()
        {
            if (DownloadData == null || !loadingAvatar)
            {
                return;
            }

            if (prevStatus != Status)
            {
                prevStatus = Status;
                if (Status != DownloadData.Status.Downloading)
                {
                    if (!FileCache.StatusText.TryGetValue(Status, out string statusText))
                        statusText = Status.ToString();

                    loadingAvatar.textMesh.text = statusText;
                    loadingAvatar.textMesh.ForceMeshUpdate(forceTextReparsing: true);
                    return;
                }
            }
            if (DownloadData.PercentageComplete == previousPercentageComplete)
                return;

            previousPercentageComplete = DownloadData.PercentageComplete;
            if (!FileCache.PercentageText.TryGetValue(previousPercentageComplete, out var text))
                text = $"{previousPercentageComplete} %";
            loadingAvatar.textMesh.text = text;
            loadingAvatar.textMesh.ForceMeshUpdate(forceTextReparsing: true);
        }
    }
}