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
using TSParser.Descriptors;
using TSParser.Service;

namespace TSParser.Tables.DvbTables
{
    public record BAT : Table
    {
        public ushort BouquetId { get; }        
        public ushort BouquetDescriptorsLenght { get; }
        public List<Descriptor> BatDescriptorList { get; } = null!;        
        public ushort TransportStreamLoopLenght { get; }
        public List<BatItem> BatTsLoopList { get; } = null!;
        public BAT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            BouquetId = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
            BouquetDescriptorsLenght = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]) & 0x0FFF);
            var pointer = 10;
            var descAllocation = $"Table: BAT, bouquet id: {BouquetId}, section number: {SectionNumber}";
            BatDescriptorList = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, BouquetDescriptorsLenght),descAllocation);
            pointer += BouquetDescriptorsLenght;            
            TransportStreamLoopLenght = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;            
            BatTsLoopList = GetBatItems(bytes.Slice(pointer, TransportStreamLoopLenght));
        }
        private List<BatItem> GetBatItems(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            List<BatItem> items = new List<BatItem>();
            while(pointer < bytes.Length)
            {
                BatItem item = new BatItem(bytes[pointer..]);
                pointer += item.TransportDescriptorsLength + 6;
                items.Add(item);
            }
            return items;
        }

        public virtual bool Equals(BAT? table)
        {
            if (table == null) return false;

            return CRC32 == table.CRC32;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return (int)CRC32;
            }
        }
        public override string ToString()
        {
            var bat = $"-=BAT=-\n";
            bat += $"   Bouquet id: {BouquetId}\n";

            bat += base.ToString();          

            bat += $"   Bouquet descriptors lenght: {BouquetDescriptorsLenght}\n";

            if (BatDescriptorList != null)
            {
                bat += $"   Bat Descriptor List count: {BatDescriptorList.Count}\n";
                foreach (var desc in BatDescriptorList)
                {
                    bat += $"{desc}";
                }
            }
            
            bat += $"   Transport stream loop lenght: {TransportStreamLoopLenght}\n";

            if(BatTsLoopList != null)
            {
                bat += $"   Bat Ts Loop List count: {BatTsLoopList.Count}\n";
                foreach (var tsloop in BatTsLoopList)
                {
                    bat += $"{tsloop}";
                }
            }
            
            bat += $"   CRC: 0x{CRC32:X}\n";
            return bat;
        }
    }

    public struct BatItem
    {
        public ushort TransportStreamId { get; } = default;
        public ushort OriginalNetworkId { get; } = default;
        public ushort TransportDescriptorsLength { get; } = default;
        public List<Descriptor> BatItemDescriptors { get; } = null!;
        public BatItem(ReadOnlySpan<byte> bytes)
        {
            TransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            OriginalNetworkId = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            TransportDescriptorsLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) & 0x0FFF);
            var descAllocation = $"Bat item ts id: {TransportStreamId}";            
            BatItemDescriptors=DescriptorFactory.GetDescriptorList(bytes.Slice(6,TransportDescriptorsLength), descAllocation);
        }
        public override string ToString()
        {
            var item = $"      Bat item Transport stream id: {TransportStreamId}\n";
            item += $"      Original network id: {OriginalNetworkId}\n";
            item += $"      Transport descriptors length: {TransportDescriptorsLength}\n";

            if(BatItemDescriptors != null)
            {
                foreach (var desc in BatItemDescriptors)
                {
                    item += $"{desc}";
                }
            }
            
            return item;
        }
    }
}
