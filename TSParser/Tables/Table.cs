// Copyright 2021 Eldar Nizamutdinov 
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

namespace TSParser.Tables
{
    public abstract record Table
    {
        private byte[] m_tableBytes = null!;
        public byte TableId { get; init; }
        public bool SectionSyntaxIndicator { get; init; }
        public ushort SectionLength { get; init; }
        public byte VersionNumber { get; init; }
        public bool CurrentNextIndicator { get; init; }
        public byte SectionNumber { get; init;  }
        public byte LastSectionNumber { get; init;  }
        public ReadOnlySpan<byte> TableBytes 
        {
            get { return m_tableBytes.AsSpan(); }
            set
            {
                m_tableBytes=new byte[value.Length];
                value.CopyTo(m_tableBytes);
            }
        }
        public uint CRC32 { get; init; }
        public Table() { }
        public Table(ReadOnlySpan<byte> bytes)
        {
            TableId = bytes[0];
            SectionSyntaxIndicator = (bytes[1] & 0x80) != 0;
            SectionLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2)) & 0x0FFF);
            VersionNumber = (byte)((bytes[5] & 0x3E) >> 1);
            CurrentNextIndicator = (bytes[5] & 0x01) != 0;
            SectionNumber = bytes[6];
            LastSectionNumber = bytes[7];
            TableBytes = bytes;            
            CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
        }

        public override string ToString()
        {
            string tbl = $"   Section syntax indicator: {SectionSyntaxIndicator}\n";
            tbl += $"   Section length: {SectionLength} bytes\n";
            tbl += $"   Table version number: {VersionNumber}\n";
            tbl += $"   Current next indicator: {CurrentNextIndicator}\n";
            tbl += $"   Section number: {SectionNumber}\n";
            tbl += $"   Last section number: {LastSectionNumber}\n";

            return tbl;
        }

        public virtual string Print(int prefixLen)
        {            
            string prefix = Utils.Prefix(prefixLen);

            string tbl = $"{prefix}Section syntax indicator: {SectionSyntaxIndicator}\n";
            tbl += $"{prefix}Section length: {SectionLength} bytes\n";
            tbl += $"{prefix}Table version number: {VersionNumber}\n";
            tbl += $"{prefix}Current next indicator: {CurrentNextIndicator}\n";
            tbl += $"{prefix}Section number: {SectionNumber}\n";
            tbl += $"{prefix}Last section number: {LastSectionNumber}\n";

            return tbl;
        }

    }
}
