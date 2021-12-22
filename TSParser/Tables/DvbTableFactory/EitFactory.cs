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
    internal class EitFactory : TableFactory
    {
        internal event EitReady OnEitReady = null!;
        private EIT m_eit = null!;
        internal EIT Eit
        {
            get { return m_eit; }
            set { m_eit = value; }
        }
        private EIT CurrentEit = null!;
        private List<EIT> eitList = new List<EIT>(100);
        internal override void PushTable(TsPacket tsPacket)
        {
            AddData(tsPacket);
            if (!IsAllTable) return;
            ParseEit();
        }
        private void ParseEit()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            var crc32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            if (Utils.GetCRC32(bytes[..^4]) != crc32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"PMT CRC incorrect!");
            }

            if (eitList.FindIndex(e => e.CRC32 == crc32) >= 0) return; // find index on crc32 base. if we have table with the same crc32, we shall drop curent table to prevent push outside duplicate tables 

            CurrentEit = new EIT(bytes);
            // next find index on table id, service id, section number and last section number based.
            // this method help us to remove old eit table from eitlist and remove potentional memory leak when new eits add to list only by crc
            var idx = eitList.FindIndex(e => e.TableId == CurrentEit.TableId &&
                                        e.ServiceId == CurrentEit.ServiceId &&
                                        e.SectionNumber == CurrentEit.SectionNumber &&
                                        e.LastSectionNumber == CurrentEit.LastSectionNumber);

            if (idx >= 0)
            {
                if (eitList[idx].VersionNumber != CurrentEit.VersionNumber)
                {
                    Logger.Send(LogStatus.Info, $"EIT version changed for service id: {eitList[idx].ServiceId}");
                    eitList.RemoveAt(idx);
                }
                else
                {
                    return;
                }
            }

            Eit = CurrentEit;
            eitList.Add(Eit);
            OnEitReady?.Invoke(Eit);

        }
    }
}
