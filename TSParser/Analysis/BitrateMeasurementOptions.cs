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
    /// <summary>
    /// Configures transport-stream bitrate measurement (window length, clock source, scope).
    /// Assign via <see cref="ParserConfig.BitrateMeasurement"/>; when <see cref="Enabled"/> is
    /// <see langword="true"/>, the analyzer runs regardless of legacy <see cref="ParserConfig.AllowAnalyzer"/>.
    /// </summary>
    public class BitrateMeasurementOptions
    {
        /// <summary>Enables bitrate measurement and activates the analyzer path.</summary>
        public bool Enabled { get; set; }

        /// <summary>Target measurement window length (for example <c>TimeSpan.FromSeconds(1)</c>).</summary>
        public TimeSpan MeasurementWindow { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Clock used to pace measurement windows.</summary>
        public BitrateClockSource ClockSource { get; set; } = BitrateClockSource.Pcr;

        /// <summary>
        /// PID carrying PCR/PTS/DTS for stream-wide measurement when using PTS or DTS, or to override
        /// auto-selected PCR-PID. When <see langword="null"/>, PCR uses the first PCR-PID; PTS/DTS use
        /// the first PID that exposes the selected timestamp.
        /// </summary>
        public ushort? ReferencePid { get; set; }

        /// <summary>Measure aggregate transport bitrate for the whole TS.</summary>
        public bool MeasureStreamBitrate { get; set; } = true;

        /// <summary>Measure transport bitrate per PID.</summary>
        public bool MeasurePerPidBitrate { get; set; } = true;

        /// <summary>Include null packets (PID <c>0x1FFF</c>) in byte counts. Default is <see langword="false"/>.</summary>
        public bool IncludeNullPackets { get; set; }

        /// <summary>
        /// Nominal transport bitrate (bit/s) for <see cref="BitrateClockSource.AssumedTransportRate"/>.
        /// Windows advance from byte volume at this rate; reported <c>BitsPerSecond</c> matches the nominal rate
        /// when the stream is CBR at this value.
        /// </summary>
        public double AssumedBitsPerSecond { get; set; } = 10_000_000;

        /// <summary>Window length in clock ticks for <see cref="ClockSource"/>.</summary>
        public ulong GetWindowTicks() => GetWindowTicks(ClockSource);

        /// <summary>Window length in clock ticks for the given clock source.</summary>
        public ulong GetWindowTicks(BitrateClockSource clockSource)
        {
            var tickRate = GetTickRate(clockSource);
            if (tickRate == 0 || MeasurementWindow <= TimeSpan.Zero)
                return 0;

            if (clockSource == BitrateClockSource.AssumedTransportRate && AssumedBitsPerSecond <= 0)
                return 0;

            return (ulong)(MeasurementWindow.TotalSeconds * tickRate);
        }

        /// <summary>Tick rate (Hz) for <see cref="ClockSource"/>.</summary>
        public ulong GetTickRate() => GetTickRate(ClockSource);

        /// <summary>Tick rate (Hz) for the given clock source.</summary>
        public static ulong GetTickRate(BitrateClockSource clockSource) =>
            clockSource switch
            {
                BitrateClockSource.Pcr => TimestampMath.PcrTickRate,
                BitrateClockSource.Pts or BitrateClockSource.Dts => TimestampMath.PtsDtsTickRate,
                BitrateClockSource.AssumedTransportRate => TimestampMath.PcrTickRate,
                _ => 0
            };

        /// <summary>Timestamp bit width for <see cref="ClockSource"/>.</summary>
        public int GetTimestampBitWidth() => GetTimestampBitWidth(ClockSource);

        /// <summary>Timestamp bit width for the given clock source.</summary>
        public static int GetTimestampBitWidth(BitrateClockSource clockSource) =>
            clockSource switch
            {
                BitrateClockSource.Pcr => TimestampMath.PcrTimestampBitWidth,
                BitrateClockSource.Pts or BitrateClockSource.Dts => TimestampMath.PtsDtsTimestampBitWidth,
                BitrateClockSource.AssumedTransportRate => TimestampMath.PcrTimestampBitWidth,
                _ => 0
            };

        /// <summary>Whether bitrate measurement can run with the current settings.</summary>
        public bool IsMeasurementConfigured() =>
            Enabled
            && MeasurementWindow > TimeSpan.Zero
            && GetWindowTicks() > 0
            && (ClockSource != BitrateClockSource.AssumedTransportRate || AssumedBitsPerSecond > 0);
    }
}

