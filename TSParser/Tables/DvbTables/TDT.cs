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
using TSParser.Enums;
using TSParser.Service;

namespace TSParser.Tables.DvbTables
{
    public record TDT : Table
    {
        public DateTime UtcDateTime { get; init; }
        public override ushort TablePid => (ushort)ReservedPids.TDT;
        public TDT(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 8)
            {
                throw new SectionParseException(
                    ParseFailureReason.BufferTooShort,
                    $"TDT section too short ({bytes.Length} bytes).");
            }

            TableId = bytes[0];
            SectionSyntaxIndicator = (bytes[1] & 0x80) != 0;
            SectionLength = SectionParseValidation.ReadSectionLength(bytes);
            if (SectionLength > SectionParseValidation.MaxSectionLength ||
                bytes.Length < SectionParseValidation.GetDeclaredSectionByteCount(SectionLength))
            {
                throw SectionParseValidation.CreateException(
                    ParseFailureReason.SectionLengthMismatch,
                    bytes.Length,
                    SectionLength);
            }

            UtcDateTime = Utils.GetDateTimeFromMJD_UTC(bytes.Slice(3, 5));
            CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
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
