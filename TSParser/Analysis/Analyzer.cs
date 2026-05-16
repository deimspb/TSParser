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

using TSParser.Analysis.Metric;
using TSParser.Service;
using TSParser.TransportStream;

namespace TSParser.Analysis
{
    public delegate void TimeStampChange(ulong timeStamp);
    public delegate void RateDelegate(ushort pid, ulong deltaPackets, ulong deltaTime);

    internal class Analyzer
    {
        private const ushort UnsetPid = 0xFFFF;

        internal event TimeStampChange OnTimeStampChange = null!;
        internal event RateDelegate OnRate = null!;
        internal event BitrateMeasuredDelegate OnBitrateMeasured = null!;

        private readonly BitrateMeasurementOptions? m_options;
        private readonly bool m_bitrateEnabled;
        private readonly BitrateClockSource m_clockSource;
        private readonly ulong m_windowTicks;
        private readonly ulong m_tickRate;
        private readonly int m_timestampBitWidth;

        private ulong m_currentTimeStamp;
        private ushort m_basePcrPid = UnsetPid;
        private ushort? m_streamReferencePid;

        private BitrateWindowMeasurer? m_streamMeasurer;
        private readonly bool m_measureUsefulAndTotalBitrate;
        private readonly Dictionary<ushort, BitrateWindowMeasurer> m_pidMeasurers = new();

        private readonly List<ushort> m_pidList = new(50);
        private readonly List<PidMetric> m_pidMetrics = new(50);

        private int m_lastPacketSize = 188;

        internal Analyzer(BitrateMeasurementOptions? options = null)
        {
            m_options = options;
            m_bitrateEnabled = options is { } o && o.IsMeasurementConfigured();

            if (!m_bitrateEnabled)
                return;

            m_clockSource = options!.ClockSource;
            m_windowTicks = options.GetWindowTicks();
            m_tickRate = options.GetTickRate();
            m_timestampBitWidth = options.GetTimestampBitWidth();
            m_measureUsefulAndTotalBitrate = options.MeasureUsefulAndTotalBitrate;

            if (options.MeasureStreamBitrate)
                m_streamMeasurer = CreateMeasurer(pid: null, trackUsefulAndTotal: m_measureUsefulAndTotalBitrate);
        }

        internal List<ushort> PidList
        {
            get
            {
                m_pidList.Sort();
                return m_pidList;
            }
        }

        internal void PushPacket(TsPacket packet) => PushPacket(packet, 188);

        internal void PushPacket(TsPacket packet, int packetSize)
        {
            if (packetSize > 0)
                m_lastPacketSize = packetSize;

            if (m_bitrateEnabled)
                PushPacketBitrate(packet, packetSize);
            else
                PushPacketLegacy(packet);
        }

        private void PushPacketLegacy(TsPacket packet)
        {
            ProcessLegacyPcr(packet);
            AddPacketToPidMetric(packet);
        }

        private void PushPacketBitrate(TsPacket packet, int packetSize)
        {
            AddPacketToPidMetric(packet);

            if (packetSize <= 0)
                return;

            if (m_measureUsefulAndTotalBitrate && m_streamMeasurer != null)
            {
                if (packet.TransportErrorIndicator)
                    return;

                if (m_clockSource == BitrateClockSource.AssumedTransportRate)
                {
                    PushAssumedBitrate(packet, packetSize);
                    return;
                }

                AddStreamBytes(packet, packetSize);

                if (m_options!.MeasurePerPidBitrate && packet.Pid != 0x1FFF)
                    GetOrCreatePidMeasurer(packet.Pid).AddBytes(packetSize);

                if (packet.HasAdaptationField && packet.Adaptation_field.DiscontinuityIndicator)
                    HandleDiscontinuity(packet.Pid);

                ProcessBitrateTimestamps(packet);
                return;
            }

            if (!ShouldCountBytes(packet))
                return;

            if (m_clockSource == BitrateClockSource.AssumedTransportRate)
            {
                PushAssumedBitrate(packet, packetSize);
                return;
            }

            m_streamMeasurer?.AddBytes(packetSize);

            if (m_options!.MeasurePerPidBitrate)
                GetOrCreatePidMeasurer(packet.Pid).AddBytes(packetSize);

            if (packet.HasAdaptationField && packet.Adaptation_field.DiscontinuityIndicator)
                HandleDiscontinuity(packet.Pid);

            ProcessBitrateTimestamps(packet);
        }

        private void PushAssumedBitrate(TsPacket packet, int packetSize)
        {
            TryEmit(AddStreamBytes(packet, packetSize));

            if (m_options!.MeasurePerPidBitrate && packet.Pid != 0x1FFF)
                TryEmit(GetOrCreatePidMeasurer(packet.Pid).AddBytes(packetSize));
        }

        private BitrateSample? AddStreamBytes(TsPacket packet, int packetSize)
        {
            if (m_streamMeasurer == null)
                return null;

            if (m_measureUsefulAndTotalBitrate)
                return m_streamMeasurer.AddBytes(packetSize, includeInUseful: packet.Pid != 0x1FFF);

            if (!ShouldCountBytes(packet))
                return null;

            return m_streamMeasurer.AddBytes(packetSize);
        }

        private void ProcessLegacyPcr(TsPacket packet)
        {
            if (m_basePcrPid == UnsetPid && packet.HasAdaptationField && packet.Adaptation_field.PCRFlag)
            {
                m_basePcrPid = packet.Pid;
                Logger.Send(LogStatus.INFO, $"PCR base pid selected: {m_basePcrPid}");
                m_currentTimeStamp = packet.Adaptation_field.PcrValue;
                OnTimeStampChange?.Invoke(m_currentTimeStamp);
            }

            if (packet.Pid == m_basePcrPid && packet.HasAdaptationField && packet.Adaptation_field.PCRFlag)
            {
                m_currentTimeStamp = packet.Adaptation_field.PcrValue;
                OnTimeStampChange?.Invoke(m_currentTimeStamp);
            }
        }

        private void ProcessBitrateTimestamps(TsPacket packet)
        {
            if (!TimestampExtractor.TryGetTimestamp(packet, m_clockSource, out var tick))
                return;

            if (m_clockSource == BitrateClockSource.Pcr)
                UpdateLegacyPcrState(packet, tick);

            if (m_options!.MeasureStreamBitrate && m_streamMeasurer != null && IsStreamClockPacket(packet))
                TryEmit(m_streamMeasurer.OnTimestamp(tick));

            if (m_options.MeasurePerPidBitrate && packet.Pid != 0x1FFF)
                TryEmit(GetOrCreatePidMeasurer(packet.Pid).OnTimestamp(tick));
        }

        private void UpdateLegacyPcrState(TsPacket packet, ulong tick)
        {
            if (m_basePcrPid == UnsetPid && packet.HasAdaptationField && packet.Adaptation_field.PCRFlag)
            {
                m_basePcrPid = packet.Pid;
                Logger.Send(LogStatus.INFO, $"PCR base pid selected: {m_basePcrPid}");
            }

            if (packet.Pid == ResolveStreamReferencePid(packet) || packet.Pid == m_basePcrPid)
            {
                if (tick != m_currentTimeStamp)
                {
                    m_currentTimeStamp = tick;
                    OnTimeStampChange?.Invoke(m_currentTimeStamp);
                }
            }
        }

        private bool IsStreamClockPacket(TsPacket packet)
        {
            if (m_options!.ReferencePid is ushort refPid)
                return packet.Pid == refPid;

            if (m_clockSource == BitrateClockSource.Pcr)
            {
                if (m_basePcrPid == UnsetPid && packet.HasAdaptationField && packet.Adaptation_field.PCRFlag)
                    m_basePcrPid = packet.Pid;

                return m_basePcrPid != UnsetPid && packet.Pid == m_basePcrPid;
            }

            if (m_streamReferencePid == null)
                m_streamReferencePid = packet.Pid;

            return packet.Pid == m_streamReferencePid;
        }

        private ushort ResolveStreamReferencePid(TsPacket packet)
        {
            if (m_options!.ReferencePid is ushort refPid)
                return refPid;

            if (m_clockSource == BitrateClockSource.Pcr)
            {
                if (m_basePcrPid == UnsetPid && packet.HasAdaptationField && packet.Adaptation_field.PCRFlag)
                    m_basePcrPid = packet.Pid;

                return m_basePcrPid;
            }

            if (m_streamReferencePid == null)
                m_streamReferencePid = packet.Pid;

            return m_streamReferencePid.Value;
        }

        private void HandleDiscontinuity(ushort pid)
        {
            if (IsStreamReferencePid(pid))
                m_streamMeasurer?.ResetWindow();

            if (m_pidMeasurers.TryGetValue(pid, out var measurer))
                measurer.ResetWindow();
        }

        private bool IsStreamReferencePid(ushort pid)
        {
            if (m_options!.ReferencePid is ushort refPid)
                return pid == refPid;

            if (m_clockSource == BitrateClockSource.Pcr)
                return m_basePcrPid != UnsetPid && pid == m_basePcrPid;

            return m_streamReferencePid != null && pid == m_streamReferencePid;
        }

        private bool ShouldCountBytes(TsPacket packet)
        {
            if (packet.TransportErrorIndicator)
                return false;

            if (packet.Pid == 0x1FFF && m_options is { IncludeNullPackets: false })
                return false;

            return true;
        }

        private void TryEmit(BitrateSample? sample)
        {
            if (sample == null)
                return;

            var value = sample.Value;
            OnBitrateMeasured?.Invoke(value);

            if (value.Pid is ushort pid && m_clockSource == BitrateClockSource.Pcr && m_options!.MeasurePerPidBitrate)
                EmitLegacyRate(pid, value);
        }

        private void EmitLegacyRate(ushort pid, BitrateSample sample)
        {
            if (m_lastPacketSize <= 0 || m_tickRate == 0)
                return;

            var deltaPackets = sample.BytesInWindow / (ulong)m_lastPacketSize;
            var deltaTicks = (ulong)(sample.WindowDuration.TotalSeconds * m_tickRate);
            if (deltaPackets == 0 || deltaTicks == 0)
                return;

            OnRate?.Invoke(pid, deltaPackets, deltaTicks);
        }

        private BitrateWindowMeasurer CreateMeasurer(ushort? pid, bool trackUsefulAndTotal = false)
        {
            double? assumedBps = m_clockSource == BitrateClockSource.AssumedTransportRate
                ? m_options!.AssumedBitsPerSecond
                : null;

            return new(m_windowTicks, m_tickRate, m_timestampBitWidth, m_clockSource, pid, assumedBps, trackUsefulAndTotal);
        }

        private BitrateWindowMeasurer GetOrCreatePidMeasurer(ushort pid)
        {
            if (!m_pidMeasurers.TryGetValue(pid, out var measurer))
            {
                measurer = CreateMeasurer(pid);
                m_pidMeasurers[pid] = measurer;
            }

            return measurer;
        }

        private void AddPacketToPidMetric(TsPacket packet)
        {
            var pidIndex = m_pidList.IndexOf(packet.Pid);

            if (pidIndex >= 0)
            {
                m_pidMetrics[pidIndex].AddPacket(packet);
                return;
            }

            m_pidList.Add(packet.Pid);
            var pm = new PidMetric(packet.Pid);
            if (!m_bitrateEnabled)
            {
                pm.OnRate += Pm_OnRate;
                OnTimeStampChange += pm.TimeStampChanged;
            }

            pm.AddPacket(packet);
            m_pidMetrics.Add(pm);
        }

        private void Pm_OnRate(ushort pid, ulong deltaPackets, ulong deltaTime)
        {
            OnRate?.Invoke(pid, deltaPackets, deltaTime);
        }
    }
}
