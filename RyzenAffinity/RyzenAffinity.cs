using MelonLoader;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[assembly: MelonInfo(typeof(Zettai.RyzenAffinity), "RyzenAffinity Guesser", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class RyzenAffinity : MelonMod
    {
        public override void OnInitializeMelon()
        {
            var affinity = GuessAffinity();
            if (affinity > 0)
                SetAffinity(affinity);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);

        private static uint GuessAffinity()
        {
            string name;
            try
            {
                name = SystemInfo.processorType;
            }
            catch (Exception)
            {
                return 0;
            }
             
            if (string.IsNullOrEmpty(name) || !name.Contains("AMD Ryzen"))
                return 0;

            uint affinityMask = GetAffinity(name);
            return affinityMask;
        }

        private static uint GetAffinity(string name)
        {
            foreach (var cpus in CpuNames)
                foreach (var item in cpus.Value)
                    if (name.Contains(item))
                        return cpus.Key;
            return 0;
        }

        private void SetAffinity(uint affinityMask)
        {
            var currentProcess = GetCurrentProcess();
            SetProcessAffinityMask(currentProcess, (IntPtr)affinityMask);
            try
            {
                // use at most the number of threads minus one thread for worker threads, but at least one 
                Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = Math.Max(1, NumberOfSetBits(affinityMask) - 1);
            }
            catch (Exception) { }
        }
        private static int NumberOfSetBits(uint i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (int)((((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
        }
        // data from https://en.wikipedia.org/wiki/List_of_AMD_Ryzen_processors
        private static readonly List<string> threeCores = new List<string>() {
           "AMD Ryzen 5 1600",
           "AMD Ryzen 5 PRO 1600",
           "AMD Ryzen 5 2600",
           "AMD Ryzen 5 3500",
           "AMD Ryzen 5 3600",
           "AMD Ryzen 9 3900",
           "AMD Ryzen 5 4500",
           "AMD Ryzen 5 4600",
           "AMD Ryzen 5 4680",
           "AMD Ryzen 5 5500U",
           "AMD Ryzen Threadripper 1920X",
           "AMD Ryzen Threadripper 2920X",
           "AMD Ryzen Threadripper 2970WX",
           "AMD Ryzen Threadripper 3960X",
           "AMD Ryzen Threadripper PRO 3945WX",
        };
        private static readonly List<string> fourCores = new List<string>() {
               "AMD Ryzen 7 PRO 1700",
               "AMD Ryzen 7 1700",
               "AMD Ryzen 7 1800",
               "AMD Ryzen 7 2700",
               "AMD Ryzen 7 3700",
               "AMD Ryzen 7 3800",
               "AMD Ryzen 9 3950",
               "AMD Ryzen 7 4700",
               "AMD Ryzen 7 4800",
               "AMD Ryzen 9 4900H",
               "AMD Ryzen 7 4980U",
               "AMD Ryzen 7 5700U",
               "AMD Ryzen Threadripper 1900X",
               "AMD Ryzen Threadripper 1950X",
               "AMD Ryzen Threadripper 2950X",
               "AMD Ryzen Threadripper 2990WX",
               "AMD Ryzen Threadripper 3970X",
               "AMD Ryzen Threadripper 3990X",
               "AMD Ryzen Threadripper PRO 3955WX",
               "AMD Ryzen Threadripper PRO 3975WX",
               "AMD Ryzen Threadripper PRO 3995WX",
        };
        private static readonly List<string> sixCores = new List<string>() {
               "AMD Ryzen 9 5900",
               "AMD Ryzen 9 PRO 5945",
               "AMD Ryzen 9 7900",
               "AMD Ryzen 9 PRO 7945",
               "AMD Ryzen 9 7845HX",
               "AMD Ryzen Threadripper PRO 5945WX",
               "AMD Ryzen Threadripper PRO 5965WX",
        };
        private static readonly List<string> eightCores = new List<string>() {
               "AMD Ryzen 9 5950X",
               "AMD Ryzen 9 7950",
               "AMD Ryzen 9 7945HX",
               "AMD Ryzen Threadripper PRO 5955WX",
               "AMD Ryzen Threadripper PRO 5975WX",
               "AMD Ryzen Threadripper PRO 5995WX",
        };
        
        private static readonly KeyValuePair<uint, List<string>>[] CpuNames = new KeyValuePair<uint, List<string>>[]
        {
            new KeyValuePair<uint, List<string>>(63,  threeCores),
            new KeyValuePair<uint, List<string>>(255,  fourCores),
            new KeyValuePair<uint, List<string>>(4095,  sixCores),
            new KeyValuePair<uint, List<string>>(65535,  eightCores),
        };
    }
}
