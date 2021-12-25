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
    public record TOT : Table
    {
        public DateTime UTCDateTime { get; }
        public ushort DescriptorLoopLength { get; }
        public List<Descriptor> TotDescriptors { get; }
        public TOT(ReadOnlySpan<byte> bytes)
        {
            TableId = bytes[0];
            SectionSyntaxIndicator = (bytes[1] & 0x80) != 0;
            SectionLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2)) & 0x0FFF);
            UTCDateTime = Utils.GetDateTimeFromMJD_UTC(bytes.Slice(3, 5));
            DescriptorLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[8..])&0x0FFF);
            var pointer = 10;
            var descAllocation = $"Table: TOT";            
            TotDescriptors = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, DescriptorLoopLength), descAllocation);
            CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            TableBytes = bytes;
        }

        public override string ToString()
        {
            var tot = "-=TOT=-\n";

            tot += $"   UTC date time: {UTCDateTime}/n";

            if (TotDescriptors != null)
            {
                tot += $"   TOT descriptors count: {TotDescriptors.Count}\n";
                foreach (var desc in TotDescriptors)
                {
                    tot += $"      {desc}\n";
                }
            }

            tot += $"   CRC32: 0x{CRC32:X}\n";

            return tot;
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            var tot = $"{headerPrefix}-=TOT=-\n";

            tot += $"{prefix}UTC date time: {UTCDateTime}/n";

            if (TotDescriptors != null)
            {
                tot += $"{prefix}TOT descriptors count: {TotDescriptors.Count}\n";
                foreach (var desc in TotDescriptors)
                {
                    tot += desc.Print(prefixLen + 4);
                }
            }

            tot += $"{prefix}TOT CRC32: 0x{CRC32:X}\n";

            return tot;
        }
        public virtual bool Equals(TOT? tot)
        {
            if (tot == null) return false;

            return CRC32 == tot.CRC32;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return (int)CRC32;
            }
        }
    }
}
