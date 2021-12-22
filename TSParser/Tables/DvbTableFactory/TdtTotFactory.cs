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
    internal class TdtTotFactory : TableFactory
    {
        internal event TdtReady OnTdtReady = null!;
        internal event Totready OnTotready = null!;

        private TDT m_tdt = null!;
        private TOT m_tot = null!;

        internal TDT Tdt
        {
            get => m_tdt;
            set => m_tdt = value;
        }

        internal TOT Tot
        {
            get => m_tot;
            set => m_tot = value;
        }


        internal override void PushTable(TsPacket tsPacket)
        {
            AddData(tsPacket);
            if (!IsAllTable) return;
            ParseTable();
        }
        private void ParseTable()
        {
            switch (TableData[0])
            {
                case 0x70:
                    {
                        ParseTDT();
                    }
                    break;
                case 0x73:
                    {
                        ParseTOT();
                    }
                    break;
                default:
                    {
                        Logger.Send(LogStatus.Warning, $"Not implement table id: {TableData[0]} for PID: 0x14");
                    }
                    break;
            }
        }

        private void ParseTDT()
        {
            Tdt = new(TableData);
            OnTdtReady?.Invoke(Tdt);
        }

        private void ParseTOT()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            var crc32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            if (Utils.GetCRC32(bytes[..^4]) != crc32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"TOT CRC incorrect!");
                return;
            }

            Tot = new(TableData);
            OnTotready?.Invoke(Tot);
        }
    }
}
