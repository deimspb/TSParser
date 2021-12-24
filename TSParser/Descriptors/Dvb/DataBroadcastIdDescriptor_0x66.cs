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

namespace TSParser.Descriptors.Dvb
{
    public record DataBroadcastIdDescriptor_0x66 : Descriptor
    {
        public ushort DataBroadcastId { get; }
        public byte[] IdSelectorByte { get; } //TODO: implement with ETSI TS 101 162
        public DataBroadcastIdDescriptor_0x66(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            DataBroadcastId = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            IdSelectorByte = new byte[DescriptorLength - 2];
            bytes.Slice(4, DescriptorLength - 2).CopyTo(IdSelectorByte);
        }

        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, data broadcast id: {DataBroadcastId}, {BitConverter.ToString(IdSelectorByte):X}";
        }
    }
}
