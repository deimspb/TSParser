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

namespace TSParser.Descriptors.Scte35Descriptors
{
    public record TimeDescriptor_0x03 : Scte35Descriptor
    {
        public ulong TaiSeconds { get; }
        public uint TaiNs { get; }
        public ushort UtcOffset { get; }
        public TimeDescriptor_0x03(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 6;
            TaiSeconds = BinaryPrimitives.ReadUInt64BigEndian(bytes[pointer..]) >> 16;
            pointer += 6;
            TaiNs = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            UtcOffset = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Time descriptor\n";
            str += $"{prefix}Tai Seconds: {TaiSeconds}\n";
            str += $"{prefix}Tai Ns: {TaiNs}\n";
            str += $"{prefix}Utc Offset: {UtcOffset}\n";

            return str;
        }
    }
}
