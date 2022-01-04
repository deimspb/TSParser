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

namespace TSParser.Tables.DvbTables
{
    public record TDT : Table
    {
        public DateTime UtcDateTime { get; init; }
        public TDT(ReadOnlySpan<byte> bytes)
        {
            TableId = bytes[0];
            SectionSyntaxIndicator = (bytes[1] & 0x80) != 0;
            SectionLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2)) & 0x0FFF);
            UtcDateTime = Utils.GetDateTimeFromMJD_UTC(bytes.Slice(3, 5));
            TableBytes = bytes;
        }
       
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            var tdt = $"{headerPrefix}-=TDT=-\n";
            tdt += $"{prefix}UTC date time: {UtcDateTime}\n";
            return tdt;
        }
    }
}
