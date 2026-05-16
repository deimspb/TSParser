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

        public BitrateSample(ushort? pid, double bitsPerSecond, ulong bytesInWindow, TimeSpan windowDuration, BitrateClockSource clockSource)
        {
            Pid = pid;
            BitsPerSecond = bitsPerSecond;
            BytesInWindow = bytesInWindow;
            WindowDuration = windowDuration;
            ClockSource = clockSource;
        }
    }
}
