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

        private ulong m_windowStartTick;
        private ulong m_bytesInWindow;
        private bool m_hasWindowStart;

        internal BitrateWindowMeasurer(
            ulong windowTicks,
            ulong tickRate,
            int timestampBitWidth,
            BitrateClockSource clockSource,
            ushort? pid = null)
        {
            m_windowTicks = windowTicks;
            m_tickRate = tickRate;
            m_timestampBitWidth = timestampBitWidth;
            m_clockSource = clockSource;
            m_pid = pid;
        }

        internal void AddBytes(int packetSize)
        {
            if (packetSize <= 0)
                return;

            m_bytesInWindow += (ulong)packetSize;
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
            if (deltaTicks < m_windowTicks || m_bytesInWindow == 0)
                return null;

            var sample = CreateSample(m_bytesInWindow, deltaTicks);

            m_windowStartTick = tick;
            m_bytesInWindow = 0;

            return sample;
        }

        internal void ResetWindow()
        {
            m_bytesInWindow = 0;
            m_hasWindowStart = false;
            m_windowStartTick = 0;
        }

        private BitrateSample CreateSample(ulong bytesInWindow, ulong deltaTicks)
        {
            var bps = TimestampMath.BitsPerSecond(bytesInWindow, deltaTicks, m_tickRate);
            var duration = TimestampMath.WindowDuration(deltaTicks, m_tickRate);
            return new BitrateSample(m_pid, bps, bytesInWindow, duration, m_clockSource);
        }
    }
}
