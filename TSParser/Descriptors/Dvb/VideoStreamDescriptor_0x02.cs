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

namespace TSParser.Descriptors.Dvb
{
    public record VideoStreamDescriptor_0x02 : Descriptor
    {
        //TODO: implement beter with ITU-T H.262 | ISO/IEC 13818-2
        public bool MultipleFrameRateFlag { get; }
        public byte FrameRateCode { get; }
        public bool Mpeg1OnlyFlag { get; }
        public bool ConstrainedParameterFlag { get; }
        public bool StillPictureFlag { get; }
        public byte ProfileAndLevelIndication { get; }
        public byte ChromaFormat { get; }
        public bool FrameRateExtensionFlag { get; }

        public VideoStreamDescriptor_0x02(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            MultipleFrameRateFlag = (bytes[pointer] & 0x80) != 0;
            FrameRateCode = (byte)((bytes[pointer] & 0x78) >> 3);
            Mpeg1OnlyFlag = (bytes[pointer] & 0x4) != 0;
            ConstrainedParameterFlag = (bytes[pointer] & 0x2) != 0;
            StillPictureFlag = (bytes[pointer++] & 0x1) != 0;
            if (!Mpeg1OnlyFlag)
            {
                ProfileAndLevelIndication = bytes[pointer++];
                ChromaFormat = (byte)((bytes[pointer] & 0xC0) >> 6);
                FrameRateExtensionFlag = (bytes[pointer] & 0x20) != 0;
                //reserved 5 bits
            }

        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Mpeg1 Only Flag: {Mpeg1OnlyFlag}, Frame Rate Code: {FrameRateCode}, Profile And Level Indication: {ProfileAndLevelIndication}";
        }
    }
}
