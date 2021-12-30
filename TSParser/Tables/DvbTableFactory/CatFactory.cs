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
    internal class CatFactory : TableFactory
    {
        internal event CatReady OnCatReady = null!;

        private CAT m_cat = null!;
        public CAT Cat
        {
            get => m_cat;
            set => m_cat = value;
        }
        private CAT CurrentCat = null!;
        private uint CurrentCRC32;
        internal override void PushTable(TsPacket tsPacket)
        {
            AddData(tsPacket);
            if (!IsAllTable) return;
            ParseTable();
        }

        private void ParseTable()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            CurrentCRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            if (Utils.GetCRC32(bytes[..^4]) != CurrentCRC32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"CAT CRC incorrect!");
                return;
            }

            if (Cat?.CRC32 == CurrentCRC32) return; // if we already have cat table and its crc32 equal curent table crc drop it. because it is the same cat 

            CurrentCat = new(bytes);

            if(Cat!=null && Cat.VersionNumber != CurrentCat.VersionNumber)
            {
                Logger.Send(LogStatus.INFO, $"Cat version changed from {Cat.VersionNumber} to {CurrentCat.VersionNumber}");
            }

            Cat = CurrentCat;
            OnCatReady?.Invoke(Cat);
        }
    }
}
