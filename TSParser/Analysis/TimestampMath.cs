// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace TSParser.Analysis
{
    /// <summary>Forward timestamp arithmetic with MPEG-style wrap.</summary>
    public static class TimestampMath
    {
        public const int PcrTimestampBitWidth = 42;
        public const int PtsDtsTimestampBitWidth = 33;

        public const ulong PcrTickRate = 27_000_000;
        public const ulong PtsDtsTickRate = 90_000;

        /// <summary>Masks a timestamp to its valid bit width.</summary>
        public static ulong Mask(ulong timestamp, int bitWidth)
        {
            if (bitWidth <= 0 || bitWidth >= 64)
                return timestamp;

            return timestamp & ((1UL << bitWidth) - 1);
        }

        /// <summary>
        /// Forward delta from <paramref name="from"/> to <paramref name="to"/>,
        /// accounting for wrap at <paramref name="bitWidth"/> bits.
        /// </summary>
        public static ulong Delta(ulong from, ulong to, int bitWidth)
        {
            var maskedFrom = Mask(from, bitWidth);
            var maskedTo = Mask(to, bitWidth);

            if (maskedTo >= maskedFrom)
                return maskedTo - maskedFrom;

            var modulus = 1UL << bitWidth;
            return modulus - maskedFrom + maskedTo;
        }

        public static double BitsPerSecond(ulong bytesInWindow, ulong deltaTicks, ulong tickRate)
        {
            if (bytesInWindow == 0 || deltaTicks == 0 || tickRate == 0)
                return 0;

            return bytesInWindow * 8.0 * tickRate / deltaTicks;
        }

        public static TimeSpan WindowDuration(ulong deltaTicks, ulong tickRate)
        {
            if (deltaTicks == 0 || tickRate == 0)
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds((double)deltaTicks / tickRate);
        }

        /// <summary>
        /// Virtual PCR ticks spanned by <paramref name="bytes"/> at an assumed constant transport bitrate.
        /// </summary>
        public static ulong AssumedDeltaTicksFromBytes(ulong bytes, ulong tickRate, double assumedBitsPerSecond)
        {
            if (bytes == 0 || tickRate == 0 || assumedBitsPerSecond <= 0)
                return 0;

            return (ulong)(bytes * 8.0 * tickRate / assumedBitsPerSecond);
        }

        /// <summary>Virtual PCR ticks spanned by a single transport packet at an assumed bitrate.</summary>
        public static double AssumedTicksFromPacket(int packetSize, ulong tickRate, double assumedBitsPerSecond)
        {
            if (packetSize <= 0 || tickRate == 0 || assumedBitsPerSecond <= 0)
                return 0;

            return packetSize * 8.0 * tickRate / assumedBitsPerSecond;
        }
    }
}
