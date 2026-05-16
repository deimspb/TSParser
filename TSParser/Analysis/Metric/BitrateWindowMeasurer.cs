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

namespace TSParser.Analysis.Metric
{
    /// <summary>
    /// Accumulates transport bytes between clock ticks and emits bitrate when the configured window elapses.
    /// </summary>
    internal sealed class BitrateWindowMeasurer
    {
        private readonly ulong m_windowTicks;
        private readonly ulong m_tickRate;
        private readonly int m_timestampBitWidth;
        private readonly BitrateClockSource m_clockSource;
        private readonly ushort? m_pid;
        private readonly double? m_assumedBitsPerSecond;
        private readonly bool m_trackUsefulAndTotal;

        private ulong m_windowStartTick;
        private ulong m_bytesInWindow;
        private ulong m_totalBytesInWindow;
        private ulong m_usefulBytesInWindow;
        private bool m_hasWindowStart;
        private double m_virtualTicks;

        internal BitrateWindowMeasurer(
            ulong windowTicks,
            ulong tickRate,
            int timestampBitWidth,
            BitrateClockSource clockSource,
            ushort? pid = null,
            double? assumedBitsPerSecond = null,
            bool trackUsefulAndTotal = false)
        {
            m_windowTicks = windowTicks;
            m_tickRate = tickRate;
            m_timestampBitWidth = timestampBitWidth;
            m_clockSource = clockSource;
            m_pid = pid;
            m_assumedBitsPerSecond = assumedBitsPerSecond;
            m_trackUsefulAndTotal = trackUsefulAndTotal;
        }

        /// <summary>Accumulates bytes; for assumed transport rate, may return a completed sample.</summary>
        internal BitrateSample? AddBytes(int packetSize, bool includeInUseful = true)
        {
            if (packetSize <= 0)
                return null;

            if (m_trackUsefulAndTotal)
            {
                m_totalBytesInWindow += (ulong)packetSize;
                if (includeInUseful)
                    m_usefulBytesInWindow += (ulong)packetSize;
            }
            else
            {
                m_bytesInWindow += (ulong)packetSize;
            }

            if (m_assumedBitsPerSecond is not double assumedBps)
                return null;

            m_virtualTicks += TimestampMath.AssumedTicksFromPacket(packetSize, m_tickRate, assumedBps);
            if (m_virtualTicks < m_windowTicks || BytesForWindowClose() == 0)
                return null;

            return CloseAssumedWindow(assumedBps);
        }

        /// <summary>Advances the measurement clock; returns a sample when the window is full.</summary>
        internal BitrateSample? OnTimestamp(ulong tick)
        {
            tick = TimestampMath.Mask(tick, m_timestampBitWidth);

            if (!m_hasWindowStart)
            {
                m_windowStartTick = tick;
                m_hasWindowStart = true;
                return null;
            }

            var deltaTicks = TimestampMath.Delta(m_windowStartTick, tick, m_timestampBitWidth);
            if (deltaTicks < m_windowTicks || BytesForWindowClose() == 0)
                return null;

            var sample = CreateSample(deltaTicks);

            m_windowStartTick = tick;
            ClearByteCounters();

            return sample;
        }

        internal void ResetWindow()
        {
            ClearByteCounters();
            m_hasWindowStart = false;
            m_windowStartTick = 0;
            m_virtualTicks = 0;
        }

        private BitrateSample CloseAssumedWindow(double assumedBps)
        {
            var bytesInWindow = BytesForWindowClose();
            var deltaTicks = TimestampMath.AssumedDeltaTicksFromBytes(bytesInWindow, m_tickRate, assumedBps);
            if (deltaTicks == 0)
                deltaTicks = 1;

            var sample = CreateSample(deltaTicks);

            ClearByteCounters();
            m_virtualTicks = 0;

            return sample;
        }

        private ulong BytesForWindowClose() =>
            m_trackUsefulAndTotal ? m_totalBytesInWindow : m_bytesInWindow;

        private void ClearByteCounters()
        {
            m_bytesInWindow = 0;
            m_totalBytesInWindow = 0;
            m_usefulBytesInWindow = 0;
        }

        private BitrateSample CreateSample(ulong deltaTicks)
        {
            if (m_trackUsefulAndTotal)
            {
                var totalBytes = m_totalBytesInWindow;
                var usefulBytes = m_usefulBytesInWindow;
                var totalBps = TimestampMath.BitsPerSecond(totalBytes, deltaTicks, m_tickRate);
                var usefulBps = TimestampMath.BitsPerSecond(usefulBytes, deltaTicks, m_tickRate);
                var duration = TimestampMath.WindowDuration(deltaTicks, m_tickRate);
                return new BitrateSample(
                    m_pid,
                    usefulBps,
                    usefulBytes,
                    duration,
                    m_clockSource,
                    totalBps,
                    usefulBps,
                    totalBytes,
                    usefulBytes);
            }

            var bytesInWindow = m_bytesInWindow;
            var bps = TimestampMath.BitsPerSecond(bytesInWindow, deltaTicks, m_tickRate);
            var windowDuration = TimestampMath.WindowDuration(deltaTicks, m_tickRate);
            return new BitrateSample(m_pid, bps, bytesInWindow, windowDuration, m_clockSource);
        }
    }
}
