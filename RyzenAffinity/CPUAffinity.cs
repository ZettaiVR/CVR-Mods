using MelonLoader;
using System;
using System.Runtime.InteropServices;

[assembly: MelonInfo(typeof(Zettai.CPUAffinity), "CPU Affinity", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class CPUAffinity : MelonMod
    {
        private static readonly MelonPreferences_Category category = MelonPreferences.CreateCategory("Zettai");
        private static readonly MelonPreferences_Entry<bool> AffinityPref = category.CreateEntry("Set CPU Affinity", true, "Set CPU Affinity");
        private static readonly MelonPreferences_Entry<bool> AffinityCpuSetsPref = category.CreateEntry("Use CpuSets insted of affinity", true, "Use CpuSets insted of affinity");
        private static readonly MelonPreferences_Entry<bool> AffinityJobWorkerPref = category.CreateEntry("Set JobWorker count", true, "Set JobWorker count");
        private static int workerCount = 0;
        private static int maxWorkerCount = 0;
        private static int cpuCount = 0;
        private static IntPtr defaultMask;
        public override void OnInitializeMelon()
        {
            if (GetProcessAffinityMask(GetCurrentProcess(), out var lpProcessAffinityMask, out var lpSystemAffinityMask) && (long)lpProcessAffinityMask != 0)
            {
                defaultMask = lpProcessAffinityMask;
            }
            maxWorkerCount = workerCount = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerMaximumCount;
            MelonLogger.Msg($"CPUAffinity: default affinity mask: {ToHexMask(defaultMask, cpuCount)}, workerCount: {workerCount}");

            AffinityPref.OnEntryValueChanged.Subscribe(ChangeModEnabled);
            AffinityJobWorkerPref.OnEntryValueChanged.Subscribe(ChangeJobWorkerEnabled);
            if (AffinityPref.Value)
                SetCpuSets();
            SetWorkerCount(AffinityJobWorkerPref.Value);
        }

        private static void ChangeModEnabled(bool oldValue, bool newValue) 
        {
            if (oldValue == newValue)
                return;

            if (newValue)
            {
                SetCpuSets();
                SetWorkerCount(AffinityJobWorkerPref.Value);
            }
            else
            {
                var process = GetCurrentProcess();
                SetProcessDefaultCpuSets(process, Span<uint>.Empty);
                if (cpuCount > 0) 
                {
                    SetProcessAffinityMask(process, defaultMask);
                }
                SetWorkerCount(false);
                MelonLogger.Msg($"DisableMod: total CPU count: {cpuCount}, assigned CPU count: {cpuCount}, affinity mask: {ToHexMask(defaultMask, cpuCount)}");
            }
        }
        private static void ChangeJobWorkerEnabled(bool oldValue, bool newValue)
        {
            if (oldValue == newValue)
                return;

            SetWorkerCount(newValue);
        }
        private static void SetCpuSets()
        {
            var currentProcess = GetCurrentProcess();
            GetSystemCpuSetInformation(IntPtr.Zero, 0, out var length, currentProcess, 0);
            Span<byte> data = stackalloc byte[(int)length];
            var success = GetSystemCpuSetInformation(data, length, out length, currentProcess, 0);
            if (!success)
            {
                MelonLogger.Msg($"SetCpuSets: success: false for second GetSystemCpuSetInformation, length {length}");
                return;
            }
            var cpuInformation = MemoryMarshal.Cast<byte, SYSTEM_CPU_SET_INFORMATION>(data);
            var totalCpuCount = cpuCount = cpuInformation.Length;
            Span<uint> ulongs = stackalloc uint[totalCpuCount];
            Span<uint> allCpus = stackalloc uint[totalCpuCount];
            int count = 0;
            ulong mask = 0;
            for (int i = 0; i < totalCpuCount; i++)
            {
                var currentCpu = cpuInformation[i];
                allCpus[count] = currentCpu.Id;
                if (currentCpu.LastLevelCacheIndex == 0 && 
                    currentCpu.NumaNodeIndex == 0 && 
                    !currentCpu.AllFlags.HasFlag(SYSTEM_CPU_SET_INFORMATION_FLAGS.Parked))
                {
                    ulongs[count] = currentCpu.Id;
                    count++;
                    mask |= (ulong)1 << i;
                }
            }

            // use at most the number of threads minus one thread for worker threads, but at least one 
            workerCount = Math.Max(1, count - 1);
            if (AffinityCpuSetsPref.Value || totalCpuCount > 64)
            {
                ulongs = ulongs.Slice(0, count);
                var result = SetProcessDefaultCpuSets(currentProcess, ulongs);
                MelonLogger.Msg($"SetCpuSets: success: {result}, total CPU count: {totalCpuCount}, assigned CPU count: {count}");
            }
            else
            {
                SetProcessAffinityMask(currentProcess, (IntPtr)mask);
                MelonLogger.Msg($"SetCpuSets: total CPU count: {totalCpuCount}, assigned CPU count: {count}, affinity mask: {ToHexMask((IntPtr)mask, cpuCount)}");
            }
        }
        private static void SetWorkerCount(bool setWorkers)
        {
            try
            {
                var count = setWorkers ? workerCount : maxWorkerCount;
                Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = count;
                MelonLogger.Msg($"SetWorkerCount: workerCount: {count}, maxWorkerCount: {maxWorkerCount}");
            }
            catch (Exception) { }
        }

        private static string ToHexMask(IntPtr mask, int count)
        {
            return mask.ToString("X" + Math.Ceiling(count / 4f).ToString());
        }

        struct SYSTEM_CPU_SET_INFORMATION
        {
            public uint Size;
            public CPU_SET_INFORMATION_TYPE Type;
            public uint Id;
            public ushort Group;
            public byte LogicalProcessorIndex;
            public byte CoreIndex;
            public byte LastLevelCacheIndex;
            public byte NumaNodeIndex;
            public byte EfficiencyClass;
            public SYSTEM_CPU_SET_INFORMATION_FLAGS AllFlags;
            public uint SchedulingClass;
            public ulong AllocationTag;
        }
        enum CPU_SET_INFORMATION_TYPE : int { CpuSetInformation }
        [Flags] enum SYSTEM_CPU_SET_INFORMATION_FLAGS : byte
        {
            None = 0,
            Parked = 1,
            Allocated = 2,
            AllocatedToTargetProcess = 4,
            RealTime = 8,
            ReservedFlag0 = 16,
            ReservedFlag1 = 32,
            ReservedFlag2 = 64,
            ReservedFlag3 = 128,
        }

        // imports

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessAffinityMask(IntPtr hProcess, out IntPtr lpProcessAffinityMask, out IntPtr lpSystemAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static unsafe extern bool SetProcessDefaultCpuSets(IntPtr Thread, uint* CpuSetIds, uint CpuSetIdCount);


        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemCpuSetInformation(
        IntPtr Information,
        ulong BufferLength,
        out ulong ReturnedLength,
        IntPtr hProcess,
        ulong Flags
            );

        private static unsafe bool GetSystemCpuSetInformation(Span<byte> Information, ulong BufferLength, out ulong ReturnedLength, IntPtr hProcess, ulong Flags)
        {
            fixed (byte* ptr = Information)
            {
                return GetSystemCpuSetInformation((IntPtr)ptr, BufferLength, out ReturnedLength, hProcess, Flags);
            }
        }
        private static unsafe bool SetProcessDefaultCpuSets(IntPtr Thread, Span<uint> CpuSetIds)
        {
            if (CpuSetIds.Length == 0)
            {
                return SetProcessDefaultCpuSets(Thread, (uint*)IntPtr.Zero, 0);
            }
            fixed (uint* ptr = CpuSetIds)
            {
                return SetProcessDefaultCpuSets(Thread, ptr, (uint)CpuSetIds.Length);
            }
        }
    }
}
