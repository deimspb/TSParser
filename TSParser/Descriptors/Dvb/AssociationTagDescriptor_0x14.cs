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
    public record AssociationTagDescriptor_0x14 : Descriptor
    {
        public ushort AssociationTag { get; }
        public ushort Use { get; }
        public byte SelectorLength { get; }
        public uint TransactionId { get; }
        public uint TimeOut { get; }
        public byte[] PrivateDataByte { get; } = null!;
        public byte[] SelectorByte { get; } = null!;
        public AssociationTagDescriptor_0x14(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            AssociationTag = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            Use = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;

            if (Use == 0x0000)
            {
                SelectorLength = bytes[pointer++];
                TransactionId = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                pointer += 4;
                TimeOut = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                pointer += 4;
            }
            else if (Use == 0x0001)
            {
                SelectorLength = bytes[pointer++];
            }
            else
            {
                SelectorLength = bytes[pointer++];
                SelectorByte = new byte[SelectorLength];
                bytes.Slice(pointer, SelectorLength).CopyTo(SelectorByte);
            }

            if (DescriptorLength - pointer > 0)
            {
                PrivateDataByte = new byte[DescriptorLength - pointer];
                bytes.Slice(pointer, DescriptorLength - pointer).CopyTo(PrivateDataByte);
            }
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, 0x{AssociationTag:X2}, Use: 0x{Use:X2}, Transaction id: 0x{TransactionId:X2}, Timeout: 0x{TimeOut:X2}";
        }
    }
}
