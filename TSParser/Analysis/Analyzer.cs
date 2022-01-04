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
    internal class Analyzer
    {
        internal event TimeStampChange OnTimeStampChange = null!;        

        private ulong m_currentTimeStamp = 0;
        private ushort m_basePcrPid = 0xFFFF;

        private List<ushort> m_pidList = new List<ushort>(50);
        private List<PidMetric> pidMetrics = new List<PidMetric>(50);
        
        internal List<ushort> PidList
        {
            get
            {
                m_pidList.Sort();
                return m_pidList;
            }
        }

        internal void PushPacket(TsPacket packet)
        {
            if (m_basePcrPid == 0xFFFF && packet.HasAdaptationField && packet.Adaptation_field.PCRFlag) // init analyzer. first packet with pcr selected as PCR based pid.
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

            var pidIndex = m_pidList.IndexOf(packet.Pid);

            if(pidIndex >= 0)
            {
                pidMetrics[pidIndex].AddPacket(packet);
            }
            else
            {
                m_pidList.Add(packet.Pid);
                var pm = new PidMetric(packet.Pid);
                OnTimeStampChange += pm.TimeStampChanged;
                pm.AddPacket(packet);
                pidMetrics.Add(pm);
            }

            
        }
    }
}
