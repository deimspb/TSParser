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

using System.Buffers.Binary;
using TSParser.Service;
using TSParser.Tables.DvbTables;
using TSParser.TransportStream;

namespace TSParser.Tables.DvbTableFactory
{
    internal class PmtFactory : TableFactory
    {
        internal event PmtReady OnPmtReady = null!;
        private PMT m_pmt=null!;    
        internal PMT Pmt
        {
            get { return m_pmt; }
            set { m_pmt = value; }
        }

        private PMT CurrentPmt=null!;
        private uint CurrentCRC32;
        internal override void PushTable(TsPacket tsPacket)
        {
            AddData(tsPacket);
            if (!IsAllTable) return;
            ParsePmt();
        }

        internal void ParsePmt()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            if (bytes[0] != 0x02)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {bytes[0]} for PMT table");
                return;
            }

            CurrentCRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);           

            if (Pmt?.CRC32 == CurrentCRC32) return; // if we already have pmt table and its crc32 equal curent table crc drop it. because it is the same pmt             

            if (Utils.GetCRC32(bytes[..^4]) != CurrentCRC32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"PMT pid {CurrentPid} CRC incorrect!");
                ResetFactory();
                return;
            }

            CurrentPmt = new PMT(bytes);

            if (Pmt != null && Pmt.VersionNumber != CurrentPmt.VersionNumber)
            {
                Logger.Send(LogStatus.INFO, $"PMT version changed from {Pmt.VersionNumber} to {CurrentPmt.VersionNumber}");
            }

            Pmt = CurrentPmt;            
            OnPmtReady?.Invoke(Pmt);
        }
    }
}
