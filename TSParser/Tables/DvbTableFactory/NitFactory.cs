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
    internal class NitFactory : TableFactory
    {
        internal event NitReady OnNitReady = null!;
        private NIT m_nit = null!;

        internal NIT Nit
        {
            get=> m_nit;
            set => m_nit = value;
        }

        private readonly Lazy<List<NIT>> nITs = new Lazy<List<NIT>>();
        private List<NIT> m_nitList =>nITs.Value;

        private NIT CurrentNit = null!;
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
                case 0x40: // nit actual
                    {
                        ParseNit();
                    }
                    break;
                case 0x41: // nit other
                    {
                        ParseNit();
                    }
                    break;
                default:
                    {
                        Logger.Send(LogStatus.ETSI, $"Not implement table id: {TableData[0]} for NIT"); 
                    }
                    break;
            }
        }

        private void ParseNit()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            var crc32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            if (Utils.GetCRC32(bytes[..^4]) != crc32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"NIT CRC incorrect!");
            }

            if (m_nitList.FindIndex(s => s.CRC32 == crc32) >= 0) return; // already push this table outside

            CurrentNit = new(bytes);

            var idx = m_nitList.FindIndex(nit=>nit.NetworkId==CurrentNit.NetworkId &&
                                          nit.SectionNumber==CurrentNit.SectionNumber &&
                                          nit.LastSectionNumber == CurrentNit.LastSectionNumber);

            if(idx >= 0)
            {
                if (m_nitList[idx].VersionNumber != CurrentNit.VersionNumber)
                {
                    Logger.Send(LogStatus.Info, $"NIT table version changed for ts id: {m_nitList[idx].NetworkId}");
                    m_nitList.RemoveAt(idx);
                }
                else
                {
                    return;
                }
            }

            Nit = CurrentNit;
            m_nitList.Add(Nit);
            OnNitReady?.Invoke(Nit);

        }
    }
}
