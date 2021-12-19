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

namespace TSParser.Tables.DvbTables
{
    public record CAT : Table
    {
        public List<Descriptor> CatDescriptorList { get; } = null!;
        public CAT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
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
        public override string ToString()
        {
            var cat = $"-=CAT=-\n";

            cat += base.ToString();            

            if(CatDescriptorList != null)
            {
                cat += $"   CAT descriptors count: {CatDescriptorList.Count}\n";
                foreach (var desc in CatDescriptorList)
                {
                    cat += $"      {desc}\n";
                }
            }
            
            cat += $"CRC: 0x{CRC32:X}\n";
            return cat;
        }
    }
}
