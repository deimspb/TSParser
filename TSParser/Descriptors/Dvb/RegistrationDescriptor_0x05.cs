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

namespace TSParser.Descriptors.Dvb
{
    public record RegistrationDescriptor_0x05 : Descriptor
    {
        public uint FormatIdentifier { get; }
        public byte[] AdditionalIdentificationInfo { get; } = null!;
        public RegistrationDescriptor_0x05(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            FormatIdentifier = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            if (DescriptorLength - pointer > 0)
            {
                AdditionalIdentificationInfo = new byte[DescriptorLength - pointer];
                bytes.Slice(pointer, DescriptorLength - pointer).CopyTo(AdditionalIdentificationInfo);
            }
        }        
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Format identifier: 0x{FormatIdentifier:X}\n";
        }
    }
}
