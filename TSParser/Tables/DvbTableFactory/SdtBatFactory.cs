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
    internal class SdtBatFactory : TableFactory
    {
        public event SdtReady OnSdtReady = null!;
        public event BatReady OnBatReady = null!;

        private SDT m_sdt = null!;
        private BAT m_bat = null!;
        private SDT CurrentSdt = null!;
        private BAT CurrentBAT = null!;

        internal SDT Sdt
        {
            get => m_sdt;
            set => m_sdt = value;
        }
        internal BAT Bat
        {
            get => m_bat;
            set => m_bat = value;
        }

        private readonly Lazy<List<SDT>> sDTs = new Lazy<List<SDT>>();
        private readonly Lazy<List<BAT>> bATs = new Lazy<List<BAT>>();
        private List<SDT> m_sdtList => sDTs.Value;
        private List<BAT> m_batList => bATs.Value;
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
                case 0x42: // sdt actual
                    {
                        ParseSdt();
                    }
                    break;
                case 0x46: // sdt other
                    {
                        ParseSdt();
                    }
                    break;
                case 0x4A: // bat
                    {
                        ParseBat();
                    }
                    break;
                default:
                    {
                        Logger.Send(LogStatus.ETSI, $"Not implement table id: {TableData[0]} for PID: 0x11"); // TODO: error in etsi 101290
                    }
                    break;
            }
        }

        private void ParseSdt()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            var crc32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            if (Utils.GetCRC32(bytes[..^4]) != crc32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"SDT CRC incorrect!");
                return;
            }

            if (m_sdtList.FindIndex(s => s.CRC32 == crc32) >= 0) return; // already push this table outside

            CurrentSdt = new SDT(bytes);

            var idx = m_sdtList.FindIndex(sdt => sdt.OriginalNetworkId == CurrentSdt.OriginalNetworkId &&
                                          sdt.TransportStreamId == CurrentSdt.TransportStreamId &&
                                          sdt.SectionNumber == CurrentSdt.SectionNumber &&
                                          sdt.LastSectionNumber == CurrentSdt.LastSectionNumber);
            if (idx >= 0)
            {
                if (m_sdtList[idx].VersionNumber != CurrentSdt.VersionNumber)
                {
                    Logger.Send(LogStatus.Info, $"SDT table version changed for ts id: {m_sdtList[idx].TransportStreamId}");
                    m_sdtList.RemoveAt(idx);
                }
                else
                {
                    return;
                }
            }

            Sdt = CurrentSdt;
            m_sdtList.Add(Sdt);
            OnSdtReady?.Invoke(Sdt);

        }
        private void ParseBat()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            var crc32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            if (Utils.GetCRC32(bytes[..^4]) != crc32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"BAT CRC incorrect!");
                return;
            }

            if (m_batList.FindIndex(b => b.CRC32 == crc32) >= 0) return; // already push this table outside

            CurrentBAT = new BAT(bytes);

            var idx = m_batList.FindIndex(bat=> bat.BouquetId==CurrentBAT.BouquetId &&
                                          bat.SectionNumber==CurrentBAT.SectionNumber &&
                                          bat.LastSectionNumber==CurrentBAT.LastSectionNumber);
            if(idx >= 0)
            {
                if (m_batList[idx].VersionNumber != CurrentBAT.VersionNumber)
                {
                    Logger.Send(LogStatus.Info, $"Bat version changed for bouquet id:{CurrentBAT.BouquetId}");
                    m_batList.RemoveAt(idx);
                }
                else
                {
                    return;
                }
            }

            Bat = CurrentBAT;
            m_batList.Add(Bat);
            OnBatReady?.Invoke(Bat);
        }
    }
}
