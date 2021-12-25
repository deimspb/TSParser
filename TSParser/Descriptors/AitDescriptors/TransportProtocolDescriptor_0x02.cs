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

namespace TSParser.Descriptors.AitDescriptors
{
    public record TransportProtocolDescriptor_0x02 : AitDescriptor
    {
        public ushort ProtocolId { get; }
        public byte TransportProtocolLabel { get; }
        public byte[] SelectorByte { get; } = null!; //TODO: implement Selector bytes  with 5.3.6.1 / 5.3.6.2 of ETSI TS 102 809 v1.3.1
        public TransportProtocolDescriptor_0x02(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ProtocolId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            TransportProtocolLabel = bytes[pointer++];
            SelectorByte = new byte[DescriptorLength - pointer + 2];
            bytes.Slice(pointer, SelectorByte.Length).CopyTo(SelectorByte);
        }

        public override string ToString()
        {
            return $"       AIT descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Protocol id: {ProtocolId}, selector bytes: {BitConverter.ToString(SelectorByte):X}\n";
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.Prefix(prefixLen);
            return $"{headerPrefix}AIT descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Protocol id: {ProtocolId}, selector bytes: {BitConverter.ToString(SelectorByte):X}\n";
        }
    }
}
