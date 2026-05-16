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
    /// <summary>Raised when a bitrate measurement window completes.</summary>
    /// <param name="sample">Measured transport bitrate for the stream or a single PID.</param>
    public delegate void BitrateMeasuredDelegate(BitrateSample sample);

    /// <summary>Bitrate measured over a completed time window.</summary>
    public readonly struct BitrateSample
    {
        /// <summary><see langword="null"/> for whole-transport measurement; otherwise the PID.</summary>
        public ushort? Pid { get; }

        /// <summary>Transport bitrate in bits per second.</summary>
        public double BitsPerSecond { get; }

        /// <summary>Transport bytes accumulated in the window (188 or 204 per packet, per parser configuration).</summary>
        public ulong BytesInWindow { get; }

        /// <summary>Actual window length derived from clock ticks in the sample.</summary>
        public TimeSpan WindowDuration { get; }

        /// <summary>Clock source that paced this window.</summary>
        public BitrateClockSource ClockSource { get; }

        /// <summary>
        /// Whole-transport bitrate including null packets (PID <c>0x1FFF</c>) when dual stream measurement is enabled.
        /// </summary>
        public double? TotalBitsPerSecond { get; }

        /// <summary>
        /// Whole-transport bitrate excluding null packets when dual stream measurement is enabled.
        /// </summary>
        public double? UsefulBitsPerSecond { get; }

        /// <summary>Bytes counted toward <see cref="TotalBitsPerSecond"/> in dual stream measurement.</summary>
        public ulong? TotalBytesInWindow { get; }

        /// <summary>Bytes counted toward <see cref="UsefulBitsPerSecond"/> in dual stream measurement.</summary>
        public ulong? UsefulBytesInWindow { get; }

        /// <summary><see langword="true"/> when <see cref="TotalBitsPerSecond"/> and <see cref="UsefulBitsPerSecond"/> are set.</summary>
        public bool HasDualStreamMeasurement => TotalBitsPerSecond.HasValue && UsefulBitsPerSecond.HasValue;

        /// <summary>
        /// Byte offset from the start of a file parse when the window closed; <see langword="null"/> for live/UDP or <see cref="TsParser.PushBytes"/>.
        /// </summary>
        public long? StreamByteOffset { get; }

        public BitrateSample(ushort? pid, double bitsPerSecond, ulong bytesInWindow, TimeSpan windowDuration, BitrateClockSource clockSource)
            : this(pid, bitsPerSecond, bytesInWindow, windowDuration, clockSource, null, null, null, null, null)
        {
        }

        public BitrateSample(
            ushort? pid,
            double bitsPerSecond,
            ulong bytesInWindow,
            TimeSpan windowDuration,
            BitrateClockSource clockSource,
            double? totalBitsPerSecond,
            double? usefulBitsPerSecond,
            ulong? totalBytesInWindow,
            ulong? usefulBytesInWindow)
            : this(pid, bitsPerSecond, bytesInWindow, windowDuration, clockSource, totalBitsPerSecond, usefulBitsPerSecond, totalBytesInWindow, usefulBytesInWindow, null)
        {
        }

        public BitrateSample(
            ushort? pid,
            double bitsPerSecond,
            ulong bytesInWindow,
            TimeSpan windowDuration,
            BitrateClockSource clockSource,
            double? totalBitsPerSecond,
            double? usefulBitsPerSecond,
            ulong? totalBytesInWindow,
            ulong? usefulBytesInWindow,
            long? streamByteOffset)
        {
            Pid = pid;
            BitsPerSecond = bitsPerSecond;
            BytesInWindow = bytesInWindow;
            WindowDuration = windowDuration;
            ClockSource = clockSource;
            TotalBitsPerSecond = totalBitsPerSecond;
            UsefulBitsPerSecond = usefulBitsPerSecond;
            TotalBytesInWindow = totalBytesInWindow;
            UsefulBytesInWindow = usefulBytesInWindow;
            StreamByteOffset = streamByteOffset;
        }

        internal BitrateSample WithStreamByteOffset(long streamByteOffset) =>
            new(
                Pid,
                BitsPerSecond,
                BytesInWindow,
                WindowDuration,
                ClockSource,
                TotalBitsPerSecond,
                UsefulBitsPerSecond,
                TotalBytesInWindow,
                UsefulBytesInWindow,
                streamByteOffset);
    }
}
