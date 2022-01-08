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

using TSParser.Descriptors;
using TSParser.Enums;
using TSParser.Service;

namespace TSParser.Tables.DvbTables
{
    public record CAT : Table
    {
        public List<Descriptor> CatDescriptorList { get; } = null!;
        public override ushort TablePid => (ushort)ReservedPids.CAT;
        public CAT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            if (TableId != 0x01)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {TableId} for CAT table");
                return;
            }
            var pointer = 8;
            var descAllocation = $"Table: CAT, section number: {SectionNumber}";
            CatDescriptorList = DescriptorFactory.GetDescriptorList(bytes[pointer..^4],descAllocation);
        }

        public virtual bool Equals(CAT? table)
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
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            var cat = $"{headerPrefix}-=CAT=-\n";

            cat += base.Print(prefixLen+2);

            if (CatDescriptorList?.Count>0)
            {
                cat += $"{prefix}CAT descriptors count: {CatDescriptorList.Count}\n";
                foreach (var desc in CatDescriptorList)
                {
                    cat += desc.Print(prefixLen +4);
                }
            }

            cat += $"{prefix}CAT CRC32: 0x{CRC32:X}\n";
            return cat;            
        }
    }
}
