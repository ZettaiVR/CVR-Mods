using Force.Crc32;
using System;
using System.Collections.Generic;

namespace Zettai
{
    internal class FastCVRDecrypt
    {
        private struct Step
        {
            public int start;

            public int length;
        }
        private struct Copies
        {
            public CopyEvent first;
            public CopyEvent second;
            public bool hasKey;
            public Copies(CopyEvent first)
            {
                this.first = first;
                second = default;
                hasKey = false;
            }
            public Copies(CopyEvent first, CopyEvent second)
            {
                this.first = first;
                this.second = second;
                hasKey = true;
            }
        }
        private struct CopyEvent
        {
            public int sourceStart;

            public int length;

            public bool sourceIsKey;

            public CopyEvent(int sourceStart, int length, bool sourceIsKey)
            {
                this.sourceStart = sourceStart;
                this.length = length;
                this.sourceIsKey = sourceIsKey;
            }
        }
        private const long RANDOM_START = 0x3FFFFFEFFFFFFF;
        private uint crc;
        private long randStart = RANDOM_START;
        private uint fragSize = 8000u;
        private readonly List<Step> steps = new List<Step>(100);
        private long Rand()
        {
            return randStart = (randStart * crc + randStart) % (long)fragSize + fragSize;
        }
        private byte[] guidBytes = new byte[36];
        public unsafe byte[] Decrypt(string guid, byte[] bytes, byte[] keyFrag)
        {
            randStart = RANDOM_START;
            if (guidBytes.Length != guid.Length)
                guidBytes = new byte[guid.Length];

            for (int i = 0; i < guid.Length; i++)
                guidBytes[i] = (byte)guid[i];

            crc = Crc32Algorithm.Compute(guidBytes);
            var originalLength = bytes.Length;
            var newLength = originalLength + 1000;
            fragSize = (uint)Math.Max(newLength / 100, 1000);
            long pos = 0L;
            int stepCount = 0;
            steps.Clear();
            while (pos < newLength)
            {
                long prnd = Rand();
                if (prnd + pos > newLength)
                    prnd = newLength - pos;

                var newStep = default(Step);
                newStep.start = (int)pos;
                newStep.length = (int)prnd;
                steps.Add(newStep);
                pos += prnd;
                stepCount++;
            }
            var ints = stackalloc int[stepCount];

            for (int i = 0; i < stepCount; i++)
                ints[i] = i;

            for (int i = 1; i < stepCount; i++)
            {
                int num4 = (int)(Rand() % (stepCount - 1) + 1);
                int num5 = ints[num4];
                ints[num4] = ints[i];
                ints[i] = num5;
            }
            var copyArray = stackalloc Copies[stepCount];
            uint offset = 0u;
            for (int i = 0; i < stepCount; i++)
            {
                var fragmentLength = steps[ints[i]].length;
                if ((offset + fragmentLength) < originalLength)
                {
                    var copyEvent = new CopyEvent((int)offset, fragmentLength, false);
                    copyArray[ints[i]] = new Copies(copyEvent);
                }
                else
                {
                    if (offset <= originalLength)
                    {
                        var inOriginal = (int)(originalLength - offset);
                        var keyLength = fragmentLength - inOriginal;
                        var copyEvent1 = new CopyEvent((int)offset, inOriginal, false);
                        var copyEvent2 = new CopyEvent(0, keyLength, true);
                        copyArray[ints[i]] = new Copies(copyEvent1, copyEvent2);
                    }
                    else
                    {
                        // afaik never happens
                        var fragOffset = (int)(offset - originalLength);
                        var copyEvent = new CopyEvent(fragOffset, fragmentLength, true);
                        copyArray[ints[i]] = new Copies(copyEvent);
                    }
                }
                offset += (uint)steps[ints[i]].length;
            }
            steps.Clear();
            byte[] outBytes = new byte[newLength];
            int length = 0;
            for (int i = 0; i < stepCount; i++)
            {
                var copyElement = copyArray[i];
                Array.Copy(copyElement.first.sourceIsKey ? keyFrag : bytes, copyElement.first.sourceStart, outBytes, length, copyElement.first.length);
                length += copyElement.first.length;
                if (copyElement.hasKey)
                {
                    Array.Copy(copyElement.second.sourceIsKey ? keyFrag : bytes, copyElement.second.sourceStart, outBytes, length, copyElement.second.length);
                    length += copyElement.second.length;
                }
            }
            return outBytes;
        }
    }
}