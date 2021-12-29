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
    public record CarouselIdentifierDescriptor_0x13 : Descriptor
    {
        public uint CarouselId { get; }        
        public byte FormatId { get; }
        public string FormatIdName=>GetFormatIdName(FormatId);
        public byte[] PrivateDataBytes { get; } = null!;
        public byte ModuleVersion { get; }
        public ushort ModuleId { get; }
        public ushort BlockSize { get; }
        public uint ModuleSize { get; }
        public byte CompressionMethod { get; }
        public uint OriginalSize { get; }
        public byte TimeOut { get; }
        public byte ObjectKeyLength { get; }
        public byte[] ObjectKeyData { get; } = null!;

        public CarouselIdentifierDescriptor_0x13(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            CarouselId = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            FormatId = bytes[pointer++];
            if(FormatId == 0x00)
            {
                var privateDataLength = DescriptorLength - 5;

                if (privateDataLength > 0)
                {
                    PrivateDataBytes = new byte[privateDataLength];
                    bytes.Slice(pointer,privateDataLength).CopyTo(PrivateDataBytes);
                }
            }
            if (FormatId == 0x01)
            {
                ModuleVersion = bytes[pointer++];
                ModuleId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
                pointer += 2;
                BlockSize = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
                pointer += 2;
                ModuleSize = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                pointer += 4;
                CompressionMethod = bytes[pointer++];
                OriginalSize = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                pointer += 4;
                TimeOut = bytes[pointer++];
                ObjectKeyLength = bytes[pointer++];
                ObjectKeyData = new byte[ObjectKeyLength];
                bytes.Slice(pointer,ObjectKeyLength).CopyTo(ObjectKeyData);
            }
            var privatedataLen = DescriptorLength - ObjectKeyLength - 21;
            if(privatedataLen > 0)
            {
                PrivateDataBytes = new byte[privatedataLen];
                bytes.Slice(pointer, privatedataLen).CopyTo(PrivateDataBytes);
            }

        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Carousel Id: {CarouselId}, {FormatIdName}";
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Carousel Id: {CarouselId}, {FormatIdName}\n";
        }
        private string GetFormatIdName(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "standard boot";
                case 0x01: return "enhanced boot";
                default: return "unknown";
            }
        }
    }
}
