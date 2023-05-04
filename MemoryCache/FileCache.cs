using ABI_RC.Systems.Safety.BundleVerifier;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Zettai
{
    internal class FileCache
    {
        internal static readonly Dictionary<DownloadData.Status, string> StatusText = new Dictionary<DownloadData.Status, string>();
        internal static readonly Dictionary<int, string> PercentageText = new Dictionary<int, string>();

        private static long downloadCountMax = 5;
        private static long downloadCount = 0;
        private static readonly Dictionary<AssetType, (string path, string extention, DirectoryInfo directoryInfo)> names = new Dictionary<AssetType, (string path, string extention, DirectoryInfo directoryInfo)>(3);
        private static readonly ConcurrentQueue<DownloadData> DiskWriteQueue  = new ConcurrentQueue<DownloadData>();
        private static readonly ConcurrentQueue<DownloadData> DiskReadQueue   = new ConcurrentQueue<DownloadData>();
        private static readonly ConcurrentQueue<DownloadData> DownloadQueue   = new ConcurrentQueue<DownloadData>();
        private static readonly ConcurrentQueue<DownloadData> HashQueue       = new ConcurrentQueue<DownloadData>();
        private static readonly ConcurrentQueue<DownloadData> DecryptQueue    = new ConcurrentQueue<DownloadData>();
        private static readonly ConcurrentQueue<DownloadData> VerifyQueue     = new ConcurrentQueue<DownloadData>();
        private static readonly ConcurrentStack<byte[]>       ByteArrayList   = new ConcurrentStack<byte[]>();

        private static int DownloadsInProgress = 0;
        private static int DecryptInProgress = 0;
        private static int HashInProgress = 0;
        private static int VerifyInProgress = 0;
        private static int WriteToDiskInProgress = 0;
        private static int ReadFromDiskInProgress = 0;

        private static readonly HashSet<FileInfo> FilesToDelete = new HashSet<FileInfo>();
        private static volatile bool AbortThreads = false;
        private static bool InitDirectoryNamesDone = false;
        const int sleepTime = 1;
        const int bufferSize = 16384;
        const double bufferSize1000Double = bufferSize * 1000d;

        private static readonly Action WriteToDiskAction = new Action(WriteToDiskTask);
        private static readonly Action ReadFromDiskAction = new Action(ReadFromDiskTask);
        private static readonly Action DownloadAction = new Action(DownloadTask);
        private static readonly Action DecryptAction = new Action(DecryptTask);
        private static readonly Action HashByteArrayAction = new Action(HashByteArrayTask);
        private static readonly Action VerifyAction = new Action(VerifyTask);

        internal static void StartTasks(int maxThreadCountDownload, int maxThreadCountDecrypt, int maxThreadCountVerify, int maxThreadCountHash) 
        {
            int diskWriteQueue = DiskWriteQueue.Count;
            int diskReadQueue = DiskReadQueue.Count;
            int downloadQueue = DownloadQueue.Count;
            int hashQueue = HashQueue.Count;
            int decryptQueue = DecryptQueue.Count;
            int verifyQueue = VerifyQueue.Count;

            if (diskWriteQueue > 0 && WriteToDiskInProgress == 0)
            {
                Task.Run(WriteToDiskAction);
            }
            if (diskReadQueue > 0 && ReadFromDiskInProgress == 0)
            {
                Task.Run(ReadFromDiskAction);
            }
            if (downloadQueue > 0)
            {
                int max = Math.Min(maxThreadCountDownload - DownloadsInProgress, downloadQueue);
                for (int i = 0; i < max; i++)
                {
                    Task.Run(DownloadAction);
                }
            }
            if (decryptQueue > 0)
            {
                int max = Math.Min(maxThreadCountDecrypt - DecryptInProgress, decryptQueue);
                for (int i = 0; i < max; i++)
                {
                    Task.Run(DecryptAction);
                }
            }
            if (hashQueue > 0)
            {
                int max = Math.Min(maxThreadCountHash - HashInProgress, hashQueue);
                for (int i = 0; i < max; i++)
                {
                    Task.Run(HashByteArrayAction);
                }
            }
            if (verifyQueue > 0)
            {
                int max = Math.Min(maxThreadCountVerify - VerifyInProgress, verifyQueue);
                for (int i = 0; i < max; i++)
                {
                    Task.Run(VerifyAction);
                }
            }
        }
        internal static void SetMaxDownloadCount(int newValue) => Interlocked.Exchange(ref downloadCountMax, newValue);
        internal static bool InitDone { get; private set; } = false;
        internal static void AbortAllThreads() => AbortThreads = true;
        internal static void Verify(DownloadData dlData) => VerifyQueue.Enqueue(dlData);
        internal static void WriteToDisk(DownloadData dlData) => DiskWriteQueue.Enqueue(dlData);
        internal static void Decrypt(DownloadData dlData) => DecryptQueue.Enqueue(dlData);
        internal static void Hash(DownloadData dlData) => HashQueue.Enqueue(dlData);
        internal static void ReadFile(DownloadData dlData) => DiskReadQueue.Enqueue(dlData);
        internal static void Download(DownloadData dlData) => DownloadQueue.Enqueue(dlData);
        internal static void DeleteOldFiles()
        {
            foreach (var file in FilesToDelete)
            {
                file.Delete();
            }
            FilesToDelete.Clear();
        }
        internal static FileCheck FileExists(AssetType type, string assetId, string fileId)
        {
            var (path, extention, directory) = names[type];
            if (File.Exists($"{path}\\{assetId}-{fileId}.{extention}"))
                return FileCheck.ExistsSameVersion;
            var fileInfos = directory.GetFiles($"{assetId}-*.{extention}", SearchOption.TopDirectoryOnly);
            if (fileInfos.Length == 0)
                return FileCheck.NotExists;
            FilesToDelete.UnionWith(fileInfos);
            return FileCheck.ExistsDifferentVersion;
        }
        internal static string GetFileCachePath(AssetType type, string assetId, string fileId)
        {
            var (path, extention, _) = names[type];
            return $"{path}\\{assetId}-{fileId}.{extention}";
        }
        private static void DecryptTask()
        {
            var sw = new System.Diagnostics.Stopwatch();
            var decrypt = new FastCVRDecrypt();
            while (!DecryptQueue.IsEmpty)
            {
                Thread.Sleep(sleepTime);
                if (!DecryptQueue.TryDequeue(out var result) || result == null)
                    continue;

                DecryptData(sw, decrypt, result);
            }
        }
        private static void DecryptData(System.Diagnostics.Stopwatch sw, FastCVRDecrypt decrypt, DownloadData result)
        {
            try
            {
                Interlocked.Increment(ref DecryptInProgress);
                sw.Restart();
                result.status = DownloadData.Status.Decrypting;
                var size = result.fileSize;

                var key = result.rawKey ?? ABI_RC.Core.IO.DownloadManagerHelperFunctions.ConvertToFileKey(result.fileKey);
                result.decryptedData = decrypt.Decrypt(result.assetId, result.rawData, key);
                sw.Stop();
                result.DecryptDone = true;
            }
            catch (Exception e)
            {
                sw.Stop();
                MelonLoader.MelonLogger.Error($"Decrypt failed! '{e.Message}' ");
                result.DecryptFailed = true;
            }
            finally
            {
                sw.Stop();
                result.DecryptTime = sw.Elapsed;
                Interlocked.Decrement(ref DecryptInProgress);
            }
        }

        private static void WriteToDiskTask()
        {
            Interlocked.Increment(ref WriteToDiskInProgress);
            var sw = new System.Diagnostics.Stopwatch();
            while (!DiskWriteQueue.IsEmpty)
            {
                if (!DiskWriteQueue.TryDequeue(out var result) || result == null)
                    continue;

                WriteToDisk(sw, result);
            }
            Interlocked.Decrement(ref WriteToDiskInProgress);
        }

        private static void WriteToDisk(System.Diagnostics.Stopwatch sw, DownloadData result)
        {
            try
            {
                sw.Restart();
                File.WriteAllBytes(result.filePath, result.rawData);
                result.FileWriteDone = true;
            }
            catch (Exception e)
            {
                sw.Stop();
                MelonLoader.MelonLogger.Error($"FileWrite failed! '{e.Message}' ");
                result.FileWriteFailed = true;
            }
            finally
            {
                sw.Stop();
                result.FileWriteTime = sw.Elapsed;
            }
        }

        private static void DownloadAsync(DownloadData data, HttpClient client) 
        {
            var httpResponseMessage = client.GetAsync(data.assetUrl, HttpCompletionOption.ResponseHeadersRead).Result;
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                MelonLoader.MelonLogger.Error($"Download failed! '{httpResponseMessage.StatusCode}' ");
                data.FileWriteFailed = true;
                return;
            }
            using (var webStream = httpResponseMessage.Content.ReadAsStreamAsync().Result)
            {
                var length = webStream.Length;
                byte[] bytes = new byte[length];
                using (var memoryStream = new MemoryStream(bytes, true))
                {
                    var totalRead = 0;
                    var buffer = new byte[bufferSize];
                    var isMoreToRead = true;
                    var rateLimit = data.rateLimit; // bytes per sec
                    var timeLimit = rateLimit > 0 ? bufferSize1000Double / rateLimit : 1d; // millisec per read event
                    var stopWatch = new System.Diagnostics.Stopwatch();
                    Interlocked.Increment(ref downloadCount);
                    do
                    {
                        stopWatch.Start();
                        var maxSlots = Interlocked.Read(ref downloadCountMax);
                        var usedSlots = Math.Max(0, Interlocked.Read(ref downloadCount)); // at least one, even when you just decreased the max count
                        var freeSlots = (maxSlots - usedSlots + 1f) / maxSlots;
                        var actualTimeLimit = timeLimit * freeSlots;
                        var bytesRead = webStream.ReadAsync(buffer, 0, buffer.Length, data.cancellationToken.Token).Result;
                        if (bytesRead == 0)
                        {
                            isMoreToRead = false;
                            data.PercentageComplete = 100;
                            data.rawData = bytes;
                            data.DownloadDone = true;
                            break;
                        }
                        memoryStream.Write(buffer, 0, bytesRead);//, data.cancellationToken.Token);
                        totalRead += bytesRead;
                        data.PercentageComplete = (int)(totalRead * 100L / length);

                        if (rateLimit == 0)
                            continue;

                        var time = stopWatch.Elapsed.Ticks / 10000d;

                        stopWatch.Reset();
                        if (time == 0d || time >= actualTimeLimit)
                            continue;

                        var _sleepTime = actualTimeLimit - time;
                        try
                        {
                            var timespan = new TimeSpan((long)(_sleepTime * 10000));
                            Task.Delay(timespan).Wait();
                        }
                        catch (ThreadAbortException)
                        {
                            MelonLoader.MelonLogger.Msg($"ThreadAbortException");
                        }
                    }
                    while (isMoreToRead);
                    Interlocked.Decrement(ref downloadCount);
                }
            }
        }
        private static void DownloadTask()
        {
            var sw = new System.Diagnostics.Stopwatch();
            using (var _httpClient = new HttpClient())
                while (!DownloadQueue.IsEmpty)
                {
                    Thread.Sleep(sleepTime);
                    if (!DownloadQueue.TryDequeue(out var result))
                        continue;                
                    Download(sw, _httpClient, result);
                }
        }
        private static void Download(System.Diagnostics.Stopwatch sw, HttpClient _httpClient, DownloadData result)
        {
            try
            {
                Interlocked.Increment(ref DownloadsInProgress);
                sw.Restart();
                DownloadAsync(result, _httpClient);
            }
            catch (Exception e)
            {
                sw.Stop();
                MelonLoader.MelonLogger.Error($"Download failed! '{e.Message}' ");
                result.FileWriteFailed = true;
            }
            finally
            {
                sw.Stop();
                result.DownloadTime = sw.Elapsed;
                Interlocked.Decrement(ref DownloadsInProgress);
            }
        }

        private static void ReadFromDiskTask()
        {
            Interlocked.Increment(ref ReadFromDiskInProgress);
            var sw = new System.Diagnostics.Stopwatch();
            while (!DiskReadQueue.IsEmpty)
            {
                Thread.Sleep(sleepTime);
                if (!DiskReadQueue.TryDequeue(out var result) || result == null || result.FileReadFailed)
                    continue;

                ReadFromDisk(sw, result);
            }
            Interlocked.Decrement(ref ReadFromDiskInProgress);
        }

        private static void ReadFromDisk(System.Diagnostics.Stopwatch sw, DownloadData result)
        {
            try
            {
                sw.Restart();
                result.status = DownloadData.Status.LoadingFromFile;
                result.rawData = File.ReadAllBytes(result.filePath);
                result.FileReadDone = true;
            }
            catch (Exception e)
            {
                sw.Stop();
                MelonLoader.MelonLogger.Error($"FileRead failed! '{e.Message}' ");
                result.FileReadFailed = true;
            }
            finally
            {
                sw.Stop();
                result.FileReadTime = sw.Elapsed;
            }
        }

   
        private static void HashByteArrayTask()
        {
            var sw = new System.Diagnostics.Stopwatch();
            byte[] hashBuffer = BorrowByteArray1M();
            while (!AbortThreads)
            {
                Thread.Sleep(sleepTime);
                if (!HashQueue.TryDequeue(out var result) || result == null || result.HashFailed)
                    continue;

                HashMD5(sw, hashBuffer, result);
            }
            ReturnByteArray1M(hashBuffer);
        }

        private static byte[] BorrowByteArray1M() 
        {
            if (ByteArrayList.Count == 0 || !ByteArrayList.TryPop(out var array))
            {
                return new byte[1048576];
            }
            return array;
        }
        private static void ReturnByteArray1M(byte[] array) 
        {
            if (array.Length == 1048576)
                ByteArrayList.Push(array);
        }

        private static void HashMD5(System.Diagnostics.Stopwatch sw, byte[] hashBuffer, DownloadData result)
        {
            try
            {
                Interlocked.Increment(ref HashInProgress);
                sw.Restart();
                if (result?.rawData == null)
                {
                    result.HashFailed = true;
                    result.FileReadFailed = true;
                    //      return;
                }
                using (var stream = new MemoryStream(result.rawData))
                using (var md5Instance = System.Security.Cryptography.MD5.Create())
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    int bytesWritten;
                    do
                    {
                        bytesWritten = stream.Read(hashBuffer, 0, hashBuffer.Length);
                        if (bytesWritten > 0)
                            md5Instance.TransformBlock(hashBuffer, 0, bytesWritten, null, 0);
                    }
                    while (bytesWritten > 0);
                    md5Instance.TransformFinalBlock(hashBuffer, 0, 0);
                    var hashResult = md5Instance.Hash;
                    result.calculatedHash = BitConverter.ToString(hashResult).Replace("-", "").ToLowerInvariant();
                    result.HashDone = true;
                }

            }
            catch (Exception e)
            {
                sw.Stop();
                MelonLoader.MelonLogger.Error($"Hash calculation failed! '{e.Message}' ");
                result.HashFailed = true;
            }
            finally
            {
                sw.Stop();
                result.MD5HashTime = sw.Elapsed;
                Interlocked.Decrement(ref HashInProgress);
            }
        }

        private static void VerifyBundle(DownloadData downloadData)
        {
            using (var bundleContext = new BundleContext(downloadData.assetUrl, downloadData.decryptedData))
            {
                if (bundleContext.IsGoodUrl)
                {
                    downloadData.Verified = true;
                    return;
                }

                if (bundleContext.IsBadUrl || !bundleContext.PreProcessBytes() || !bundleContext.CompleteVerification())
                {
                    downloadData.decryptedData = null;
                    downloadData.Verified = false;
                    return;
                }
                downloadData.Verified = true;
            }
        }
        internal static bool ShouldVerify(DownloadData downloadData)
        {
            if (downloadData.type == AssetType.Scene|| downloadData.isLocal)
            {
                downloadData.Verified = true;
                downloadData.VerifyDone = true;
                return false;
            }
            if (!MemoryCache.verifierEnabled)
            {
                downloadData.Verified = true;
                downloadData.VerifyDone = true;
                return false;
            }

            var shouldVerify = (!MemoryCache.publicOnly || MemoryCache.isPublic);
            if (!shouldVerify)
            {
                downloadData.Verified = true;
                downloadData.VerifyDone = true;
            }
            return shouldVerify;
        }
   
        private static void VerifyTask()
        {
            var sw = new System.Diagnostics.Stopwatch();
            while (!VerifyQueue.IsEmpty)
            {
                Thread.Sleep(sleepTime);
                if (!VerifyQueue.TryDequeue(out var result) || result == null || result.FileReadFailed)
                    continue;
                Verify(sw, result);
            }
        }
        private static void Verify(System.Diagnostics.Stopwatch sw, DownloadData result)
        {
            try
            {
                Interlocked.Increment(ref VerifyInProgress);
                sw.Restart();
                result.status = DownloadData.Status.BundleVerifier;
                VerifyBundle(result);
                result.VerifyDone = true;
            }
            catch (Exception e)
            {
                sw.Stop();
                result.Verified = false;
                result.VerifyFailed = true;
                MelonLoader.MelonLogger.Error($"Verify failed! '{e.Message}' ");
            }
            finally
            {
                sw.Stop();
                result.VerifyTime = sw.Elapsed;
                Interlocked.Decrement(ref VerifyInProgress);
            }
        }

         
        internal enum FileCheck
        {
            ExistsSameVersion,
            ExistsDifferentVersion,
            NotExists
        }
        internal static void InitDirectoryNames()
        {
            if (InitDirectoryNamesDone || 
                string.IsNullOrEmpty(ABI_RC.Core.Savior.MetaPort.Instance?.APPLICATION_DATAPATH) ||
                !Directory.Exists(ABI_RC.Core.Savior.MetaPort.Instance.APPLICATION_DATAPATH))
                return;

            var dataPath = ABI_RC.Core.Savior.MetaPort.Instance.APPLICATION_DATAPATH;
            var AvatarsDir = dataPath + "/Avatars";
            var WorldsDir = dataPath + "/Worlds";
            var PropsDir = dataPath + "/Spawnables";

            var Avatars = Directory.CreateDirectory(AvatarsDir);
            var Worlds = Directory.CreateDirectory(WorldsDir);
            var Props = Directory.CreateDirectory(PropsDir);

            names[AssetType.Avatar] = (AvatarsDir, "cvravatar", Avatars);
            names[AssetType.Scene] = (WorldsDir, "cvrworld", Worlds);
            names[AssetType.Prop] = (PropsDir, "cvrprop", Props);

            InitDirectoryNamesDone = true;
        }
        internal static void Init(int downloadThreads)
        {
            SetMaxDownloadCount(downloadThreads);


            for (int i = 0; i < 100; i++)
            {
                PercentageText[i] = $"Downloading {i} %";
            }

            StatusText[DownloadData.Status.None] = "Not started";
            StatusText[DownloadData.Status.NotStarted] = "Not started";
            StatusText[DownloadData.Status.DownloadQueued] = "Download queued";
            StatusText[DownloadData.Status.Downloading] = "Downloading";
            StatusText[DownloadData.Status.LoadingFromFileQueued] = "Loading file queued";
            StatusText[DownloadData.Status.LoadingFromFile] = "Loading file";
            StatusText[DownloadData.Status.HashingQueued] = "Hashing queued";
            StatusText[DownloadData.Status.Hashing] = "Hashing";
            StatusText[DownloadData.Status.DecryptingQueued] = "Decrypting queued";
            StatusText[DownloadData.Status.Decrypting] = "Decrypting";
            StatusText[DownloadData.Status.BundleVerifierQueued] = "BundleVerifier queued";
            StatusText[DownloadData.Status.BundleVerifier] = "BundleVerifier";
            StatusText[DownloadData.Status.LoadingBundleQueued] = "Loading bundle queued";
            StatusText[DownloadData.Status.LoadingBundle] = "Loading bundle";
            StatusText[DownloadData.Status.Done] = "Done";
            StatusText[DownloadData.Status.Error] = "Error";
            InitDone = true;
            InitDirectoryNames();
        }
    }
}