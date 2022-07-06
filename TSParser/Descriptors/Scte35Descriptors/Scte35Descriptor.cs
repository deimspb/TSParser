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
using TSParser.DictionariesData;
using TSParser.Service;

namespace TSParser.Descriptors.Scte35Descriptors
{
    public record Scte35Descriptor : Descriptor
    {
        public new string DescriptorName => Dictionaries.GetSpliceInfoDescriptorName(DescriptorTag);
        public uint Identifier { get; }
        public byte[] PrivateByte { get; }
        public Scte35Descriptor(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Identifier = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            var privateByteLength = DescriptorLength - 4;
            if (privateByteLength > 0)
            {
                PrivateByte = new byte[privateByteLength];
                bytes.Slice(pointer, privateByteLength).CopyTo(PrivateByte);
            }
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Splice descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {DescriptorLength}\n";
        }
    }
}
