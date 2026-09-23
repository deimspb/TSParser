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
        public event SdtReady? OnSdtReady;
        public event BatReady? OnBatReady;

        private readonly SectionTableCache<SDT, (ushort OriginalNetworkId, ushort TransportStreamId, byte SectionNumber, byte LastSectionNumber)> _sdtCache = new();
        private readonly SectionTableCache<BAT, (ushort BouquetId, byte SectionNumber, byte LastSectionNumber)> _batCache = new();
        private SDT? m_sdt;
        private BAT? m_bat;

        internal SDT? Sdt
        {
            get => m_sdt;
            set => m_sdt = value;
        }
        internal BAT? Bat
        {
            get => m_bat;
            set => m_bat = value;
        }

        internal override void PushTable(TsPacket tsPacket)
        {
            ProcessAssembledSections(tsPacket);
        }

        protected override void ProcessCurrentSection()
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
                ReportSectionCrcFailed(bytes[0]);
                Logger.Send(LogStatus.ETSI, $"SDT CRC incorrect!");
                ResetFactory();
                return;
            }

            if (_sdtCache.HasCrc(crc32)) return; // already push this table outside

            if (!TryParseAssembledTable(() =>
            {
                var currentSdt = new SDT(TableData);
                var key = (
                    currentSdt.OriginalNetworkId,
                    currentSdt.TransportStreamId,
                    currentSdt.SectionNumber,
                    currentSdt.LastSectionNumber);

                if (!_sdtCache.TryAccept(
                    currentSdt,
                    key,
                    dropSameVersionForSameKey: true,
                    (previous, _) => $"SDT table version changed for ts id: {previous.TransportStreamId}"))
                {
                    return;
                }

                Sdt = currentSdt;
                OnSdtReady?.Invoke(currentSdt);
            }, "SDT"))
            {
                return;
            }
        }
        private void ParseBat()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            var crc32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);            

            if (Utils.GetCRC32(bytes[..^4]) != crc32) // drop invalid ts packet
            {
                ReportSectionCrcFailed(bytes[0]);
                Logger.Send(LogStatus.ETSI, $"BAT CRC incorrect!");
                ResetFactory();
                return;
            }

            if (_batCache.HasCrc(crc32)) return; // already push this table outside

            if (!TryParseAssembledTable(() =>
            {
                var currentBat = new BAT(TableData);
                var key = (
                    currentBat.BouquetId,
                    currentBat.SectionNumber,
                    currentBat.LastSectionNumber);

                if (!_batCache.TryAccept(
                    currentBat,
                    key,
                    dropSameVersionForSameKey: true,
                    (_, current) => $"Bat version changed for bouquet id:{current.BouquetId}"))
                {
                    return;
                }

                Bat = currentBat;
                OnBatReady?.Invoke(currentBat);
            }, "BAT"))
            {
                return;
            }
        }
    }
}
