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

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSParser.DictionariesData;

namespace TSParser.Descriptors.ExtendedDvb
{
    public record ImageIconDescriptor_0x00 : Descriptor
    {
        public byte DescriptorTagExtension { get; }
        public string ExtensionDescriptorName => Dictionaries.GetExtendedDescriptorName(DescriptorTagExtension);
        public byte DescriptorNumber { get; }
        public byte LastDescriptorNumber { get; }
        public byte IconId { get; }
        public byte IconTransportMode { get; }
        public bool PositionFlag { get; }
        public byte CoordinateSystem { get; }
        public ushort IconHorizontalOrigin { get; }
        public ushort IconVerticalOrigin { get; }
        public byte IconTypeLength { get; }
        public string IconTypeChar { get; } = null!;
        public byte IconDataLength { get; }
        public byte[] IconDataByte { get; } = null!;
        public byte UrlLength { get; }
        public string UrlChar { get; } = null!;
        public ImageIconDescriptor_0x00(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            DescriptorTagExtension = bytes[pointer++];
            DescriptorNumber = (byte)((bytes[pointer] & 0xF0) >> 4);
            LastDescriptorNumber = (byte)(bytes[pointer++] & 0x0F);
            //reserved 5 bits
            IconId = (byte)(bytes[pointer++] & 0x07);

            if (DescriptorNumber == 0x00)
            {
                IconTransportMode = (byte)((bytes[pointer] & 0xC0) >> 6);
                PositionFlag = (bytes[pointer] & 0x20) != 0;

                if (PositionFlag)
                {
                    CoordinateSystem = (byte)((bytes[pointer++] & 0x1C) >> 2);
                    //reserved 2 bits
                    IconHorizontalOrigin = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer++..]) >> 4);
                    IconVerticalOrigin = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
                    pointer += 2;
                }
                else
                {
                    //reserved 5 bits
                    pointer++;
                }
                IconTypeLength = bytes[pointer++];
                IconTypeChar = Dictionaries.BytesToString(bytes.Slice(pointer, IconTypeLength));
                pointer += IconTypeLength;

                if (IconTransportMode == 0x00)
                {
                    IconDataLength = bytes[pointer++];
                    IconDataByte = new byte[IconDataLength];
                    bytes.Slice(pointer, IconDataLength).CopyTo(IconDataByte);
                    pointer += IconDataLength;
                }
                else if(IconTransportMode == 0x01)
                {
                    UrlLength = bytes[pointer++];
                    UrlChar = Dictionaries.BytesToString(bytes.Slice(pointer, UrlLength));
                    pointer += UrlLength;
                }
            }
            else
            {
                IconDataLength = bytes[pointer++];
                IconDataByte = new byte[IconDataLength];
                bytes.Slice(pointer, IconDataLength).CopyTo(IconDataByte);
                pointer += IconDataLength;
            }
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {ExtensionDescriptorName}, Icon type: {IconTypeChar}, Icon url: {UrlChar}";
        }
    }
}
