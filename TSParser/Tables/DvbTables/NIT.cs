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
    public record NIT : Table
    {
        public ushort NetworkId { get; }        
        public ushort NetworkDescriptorsLenght { get; }
        public List<Descriptor> NitDescriptorList { get; } = null!;        
        public ushort TransportStreamLoopLenght { get; }
        public List<TransportStreamLoop> TransportStreamLoops { get; } = null!;
        public NIT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            NetworkId = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
            NetworkDescriptorsLenght = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]) & 0x0FFF);
            var pointer = 10;
            var descAllocation = $"Table: NIT, table id: {TableId}, section number: {SectionNumber}";
            NitDescriptorList = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer,NetworkDescriptorsLenght), descAllocation);
            pointer += NetworkDescriptorsLenght;
            TransportStreamLoopLenght = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            TransportStreamLoops = GetTransportStreamLoops(bytes.Slice(pointer,TransportStreamLoopLenght));
        }
        private List<TransportStreamLoop> GetTransportStreamLoops(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            List<TransportStreamLoop> items = new List<TransportStreamLoop>();
            while(pointer < bytes.Length)
            {
                TransportStreamLoop item = new TransportStreamLoop(bytes[pointer..]);
                pointer += item.TransportDescriptorsLength + 6;
                items.Add(item);
            }
            return items;
        }
        public virtual bool Equals(NIT? table)
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
            var nit = $"-=NIT=-\n";
            nit += $"   Network id: {NetworkId}\n";
            nit += base.ToString();
            nit += $"   Network descriptors lenght: {NetworkDescriptorsLenght}\n";
            if(NitDescriptorList != null)
            {
                nit += $"   Nit Descriptor List count: {NitDescriptorList.Count}\n";
                foreach (var desc in NitDescriptorList)
                {
                    nit += $"      {desc}\n";
                }
            }            
            nit += $"   Transport stream loop lenght: {TransportStreamLoopLenght}\n";
            if (TransportStreamLoops != null)
            {
                nit += $"   Transport Stream Loops count: {TransportStreamLoops.Count}\n";
                foreach (var loop in TransportStreamLoops)
                {
                    nit += $"{loop}\n";
                }
            }
            return nit;
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            var nit = $"{header}-=NIT=-\n";
            nit += $"{prefix}Network id: {NetworkId}\n";
            nit += base.Print(prefixLen + 2);
            nit += $"{prefix}Network descriptors lenght: {NetworkDescriptorsLenght}\n";
            if (NetworkDescriptorsLenght > 0)
            {
                nit += $"{prefix}Nit Descriptor List count: {NitDescriptorList.Count}\n";
                foreach (var desc in NitDescriptorList)
                {
                    nit += desc.Print(prefixLen + 4);
                }
            }
            nit += $"{prefix}Transport stream loop lenght: {TransportStreamLoopLenght}\n";
            if (TransportStreamLoopLenght > 0)
            {
                nit += $"{prefix}Transport Stream Loops count: {TransportStreamLoops.Count}\n";
                foreach (var loop in TransportStreamLoops)
                {
                    nit += loop.Print(prefixLen + 4);
                }
            }
            return nit;
        }
    }
    public struct TransportStreamLoop
    {
        public ushort TransportStreamId { get; } = default;
        public ushort OriginalNetworkId { get; } = default;
        public ushort TransportDescriptorsLength { get; } = default;
        public List<Descriptor> TransportStreamLoopDescriptors { get; } = null!;
        public TransportStreamLoop(ReadOnlySpan<byte> bytes)
        {
            TransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            OriginalNetworkId= BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            TransportDescriptorsLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) & 0x0FFF);
            var descAllocation = $"Table: NIT, ts loop, ts id: {TransportStreamId}";
            TransportStreamLoopDescriptors = DescriptorFactory.GetDescriptorList(bytes.Slice(6,TransportDescriptorsLength),descAllocation);
        }

        public override string ToString()
        {
            var loop = $"      Transport stream id: {TransportStreamId}\n";
            loop += $"      Original network id: {OriginalNetworkId}\n";
            loop += $"      Transport descriptors length: {TransportDescriptorsLength}\n";
            if(TransportStreamLoopDescriptors != null)
            {
                foreach (var desc in TransportStreamLoopDescriptors)
                {
                    loop += $"         {desc}\n";
                }
            }
            
            return loop;
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            var loop = $"{header}Transport stream id: {TransportStreamId}\n";
            loop += $"{prefix}Original network id: {OriginalNetworkId}\n";
            loop += $"{prefix}Transport descriptors length: {TransportDescriptorsLength}\n";
            if (TransportStreamLoopDescriptors != null)
            {
                foreach (var desc in TransportStreamLoopDescriptors)
                {
                    loop += desc.Print(prefixLen + 4);
                }
            }

            return loop;
        }
    }
}
