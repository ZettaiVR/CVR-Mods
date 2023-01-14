using ABI_RC.Core.IO;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.Safety.BundleVerifier;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Zettai
{
    internal class FileCache
    {
        private static readonly Dictionary<DownloadTask.ObjectType, (string path, string extention, DirectoryInfo directoryInfo)> names = new Dictionary<DownloadTask.ObjectType, (string path, string extention, DirectoryInfo directoryInfo)>(3);
        internal static readonly Dictionary<int, string> PercentageText = new Dictionary<int, string>();
        private static readonly System.Collections.Concurrent.ConcurrentQueue<DownloadData> DiskWriteQueue = new System.Collections.Concurrent.ConcurrentQueue<DownloadData>();
        private static readonly System.Collections.Concurrent.ConcurrentQueue<DownloadData> DiskReadQueue = new System.Collections.Concurrent.ConcurrentQueue<DownloadData>();
        private static readonly System.Collections.Concurrent.ConcurrentQueue<DownloadData> HashQueue = new System.Collections.Concurrent.ConcurrentQueue<DownloadData>();
        private static readonly System.Collections.Concurrent.ConcurrentQueue<DownloadData> DecryptQueue = new System.Collections.Concurrent.ConcurrentQueue<DownloadData>();
        private static readonly System.Collections.Concurrent.ConcurrentQueue<DownloadData> VerifyQueue = new System.Collections.Concurrent.ConcurrentQueue<DownloadData>();
        private static readonly HashSet<FileInfo> FilesToDelete = new HashSet<FileInfo>();
        private static readonly List<Thread> threads = new List<Thread>();
        private static volatile bool AbortThreads = false;
        private static readonly byte[] hashBuffer = new byte[1048576];
        const int sleepTime = 10;

        internal static bool InitDone { get; private set; } = false;
        internal static void AbortAllThreads() => AbortThreads = true;
        internal static void Verify(DownloadData dlData) => VerifyQueue.Enqueue(dlData);
        internal static void WriteToDisk(DownloadData dlData) => DiskWriteQueue.Enqueue(dlData);
        internal static void Decrypt(DownloadData dlData) => DecryptQueue.Enqueue(dlData);
        internal static void Hash(DownloadData dlData) => HashQueue.Enqueue(dlData);
        internal static void ReadFile(DownloadData dlData) => DiskReadQueue.Enqueue(dlData);
        internal static void DeleteOldFiles()
        {
            foreach (var file in FilesToDelete)
            {
                file.Delete();
            }
            FilesToDelete.Clear();
        }
        internal static FileCheck FileExists(DownloadTask.ObjectType type, string assetId, string fileId)
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
        internal static string GetFileCachePath(DownloadTask.ObjectType type, string assetId, string fileId)
        {
            var (path, extention, _) = names[type];
            return $"{path}\\{assetId}-{fileId}.{extention}";
        }
        private static void DecryptThread()
        {
            while (true && !AbortThreads)
            {
                Thread.Sleep(sleepTime);
                if (!DecryptQueue.TryDequeue(out var result))
                    continue;

                try
                {
                    var decrypt = new ABI_RC.Core.CVRDecrypt();
                    var key = DownloadManagerHelperFunctions.ConvertToFileKey(result.fileKey);
                    result.decryptedData = decrypt.Decrypt(result.assetId, result.rawData, key);
                    result.DecryptDone = true;
                }
                catch (Exception e)
                {
                    MelonLoader.MelonLogger.Error($"Decrypt failed! '{e.Message}' ");
                    result.DecryptFailed = true;
                }
            }
        }
        private static void WriteToDiskThread()
        {
            while (true && !AbortThreads)
            {
                Thread.Sleep(sleepTime);
                if (!DiskWriteQueue.TryDequeue(out var result))
                    continue;

                try
                {
                    File.WriteAllBytes(result.filePath, result.rawData);
                    result.FileWriteDone = true;
                }
                catch (Exception e)
                {
                    MelonLoader.MelonLogger.Error($"FileWrite failed! '{e.Message}' ");
                    result.FileWriteFailed = true;
                }
            }
        }
        private static void ReadFromDiskThread()
        {
            while (true && !AbortThreads)
            {
                Thread.Sleep(sleepTime);
                if (!DiskReadQueue.TryDequeue(out var result) || result.FileReadFailed)
                    continue;

                try
                {
                    result.rawData = File.ReadAllBytes(result.filePath);
                    result.FileReadDone = true;
                }
                catch (Exception e)
                {
                    MelonLoader.MelonLogger.Error($"FileRead failed! '{e.Message}' ");
                    result.FileReadFailed = true;
                }
            }
        }
        private static void HashByteArrayThread()
        {
            while (true && !AbortThreads)
            {
                Thread.Sleep(sleepTime);
                if (!HashQueue.TryDequeue(out var result) || result.HashFailed)
                    continue;
              
                try
                {
                    if (result?.rawData == null)
                    {
                        result.HashFailed = true;
                        result.FileReadFailed = true;
                        continue;
                    }
                    var stream = new MemoryStream(result.rawData);
                    stream.Seek(0, SeekOrigin.Begin);
                    using (var md5Instance = System.Security.Cryptography.MD5.Create())
                    {
                        int bytesWritten;
                        do
                        {
                            bytesWritten = stream.Read(hashBuffer, 0, 1048576);
                            if (bytesWritten > 0)
                            {
                                md5Instance.TransformBlock(hashBuffer, 0, bytesWritten, null, 0);
                            }
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
                    MelonLoader.MelonLogger.Error($"Hash calculation failed! '{e.Message}' ");
                    result.HashFailed = true;
                }
            }
        }
        private static void VerifyBundle(DownloadData downloadData)
        {
            using (BundleContext bundleContext = new BundleContext(downloadData.assetUrl, downloadData.decryptedData))
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
            if (downloadData.type == DownloadTask.ObjectType.World || downloadData.isLocal)
            {
                downloadData.Verified = true;
                downloadData.VerifyDone = true;
                return false;
            }
            if (!MetaPort.Instance.settings.GetSettingsBool("ExperimentalBundleVerifierEnabled", false))
            {
                downloadData.Verified = true;
                downloadData.VerifyDone = true;
                return false;
            }
            // what is an enum even
            bool? userBundleVerifierBypass = MetaPort.Instance.SelfModerationManager.GetUserBundleVerifierBypass(downloadData.target);
            if (userBundleVerifierBypass != null)
            {
                return !userBundleVerifierBypass.Value;
            }

            bool friendsWith = MemoryCache.FriendsWith(downloadData.target);
            bool publicOnly = MetaPort.Instance.settings.GetSettingsBool("ExperimentalBundleVerifierPublicsOnly", false);
            bool filterFirends = MetaPort.Instance.settings.GetSettingsBool("ExperimentalBundleVerifierFilterFriends", false);
            bool isPublic = string.Equals(MetaPort.Instance.CurrentInstancePrivacy, "public", StringComparison.OrdinalIgnoreCase);

            var shouldVerify = (!publicOnly || isPublic) && (filterFirends || !friendsWith);
            if (!shouldVerify)
            {
                downloadData.Verified = true;
                downloadData.VerifyDone = true;
            }
            return shouldVerify;
        }
        private static void VerifyThread()
        {
            while (true && !AbortThreads)
            {
                Thread.Sleep(sleepTime);
                if (!VerifyQueue.TryDequeue(out var result) || result.FileReadFailed)
                    continue;

                try
                {
                    VerifyBundle(result);
                    result.VerifyDone = true;
                }
                catch (Exception e)
                {
                    result.Verified = false;
                    result.VerifyFailed = true;
                    MelonLoader.MelonLogger.Error($"Verify failed! '{e.Message}' ");
                }
            }
        }
        private static void StartNewThreads(string name, ThreadStart threadStart, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var thread = new Thread(threadStart)
                {
                    IsBackground = true,
                    Name = name
                };
                threads.Add(thread);
                thread.Priority = ThreadPriority.BelowNormal;
                thread.Start();
            }
        }
        internal enum FileCheck
        {
            ExistsSameVersion,
            ExistsDifferentVersion,
            NotExists
        }
        internal static IEnumerator Init()
        {
            var threadCount = Environment.ProcessorCount > 10 ? 2 : 1;
            StartNewThreads("FileWriteThread", WriteToDiskThread);
            StartNewThreads("HashThread", HashByteArrayThread, threadCount);
            StartNewThreads("FileReadThread", ReadFromDiskThread);
            StartNewThreads("DecryptThread", DecryptThread, threadCount);
            StartNewThreads("VerifyThread", VerifyThread, threadCount);

            while (string.IsNullOrEmpty(MetaPort.Instance.APPLICATION_DATAPATH) || !Directory.Exists(MetaPort.Instance.APPLICATION_DATAPATH))
                yield return null;

            var dataPath = MetaPort.Instance.APPLICATION_DATAPATH;
            var AvatarsDir = dataPath + "/Avatars";
            var WorldsDir = dataPath + "/Worlds";
            var PropsDir = dataPath + "/Spawnables";

            var Avatars = Directory.CreateDirectory(AvatarsDir);
            var Worlds = Directory.CreateDirectory(WorldsDir);
            var Props = Directory.CreateDirectory(PropsDir);

            names[DownloadTask.ObjectType.Avatar] = (AvatarsDir, "cvravatar", Avatars);
            names[DownloadTask.ObjectType.World] = (WorldsDir, "cvrworld", Worlds);
            names[DownloadTask.ObjectType.Prop] = (PropsDir, "cvrprop", Props);

            for (int i = 0; i < 100; i++)
            {
                PercentageText[i] = $"{i} %";
            }

            InitDone = true;
        }
    }

}