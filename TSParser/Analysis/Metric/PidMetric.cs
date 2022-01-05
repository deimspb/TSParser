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

using TSParser.Service;
using TSParser.TransportStream;

namespace TSParser.Analysis.Metric
{
    
    public class PidMetric
    {
        public event RateDelegate OnRate = null!;
        public ushort Pid { get; }
        public ulong CurrentPacketCounter { get; private set; }

        private ulong m_lastPacketCounter;

        private ulong LastTimeStamp = 0;
        private byte LastCC;
        private ulong m_ccErrorCount;

        private ulong m_gate = 100 * 27000;//msec * 27000
        public ulong CCErrorCount
        {
            get => m_ccErrorCount;
            set
            {
                m_ccErrorCount = value;
                Logger.Send(LogStatus.ETSI, $"CC detect om pid: {Pid}, Total CC for this pid: {m_ccErrorCount}");
            }
        }
        public PidMetric(ushort pid)
        {
            Pid = pid;
        }

        public void AddPacket(TsPacket packet)
        {
            if (packet.Pid != Pid)
            {
                Logger.Send(LogStatus.WARNING, $"Pid changed from {Pid} to {packet.Pid} packet count: {CurrentPacketCounter}");
            }

            if (packet.TransportErrorIndicator)
            {
                Logger.Send(LogStatus.ETSI, $"Transport stream indicator for pid {Pid}");
                return;
            }

            CheckCC(packet);
            LastCC = packet.ContinuityCounter;
            CurrentPacketCounter++;
        }

        private void CheckCC(TsPacket tsPacket)
        {
            if (CurrentPacketCounter == 0) return;

            if (tsPacket.Pid == 0x1fff) return;

            if (LastCC <= 14)
            {
                if (LastCC + 1 != tsPacket.ContinuityCounter)
                {
                    CCErrorCount++;
                }
            }
            else
            {
                if (LastCC == 15)
                {
                    if (tsPacket.ContinuityCounter != 0)
                    {
                        CCErrorCount++;
                    }
                }
            }
        }

        public void TimeStampChanged(ulong timeStamp)
        {

            if (CurrentPacketCounter != 0 && m_lastPacketCounter != 0 && timeStamp != LastTimeStamp)
            {
                var deltaP = (CurrentPacketCounter - m_lastPacketCounter);
                var deltaT = timeStamp - LastTimeStamp;
                if (deltaT < m_gate) return;

                OnRate?.Invoke(Pid, deltaP, deltaT);               
            }

            LastTimeStamp = timeStamp;
            m_lastPacketCounter = CurrentPacketCounter;
        }
    }
}
