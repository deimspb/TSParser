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
    public record AvcTimingAndHrdDescriptor_0x2A : Descriptor
    {
        public bool HrdManagementValidFlag { get; }
        public bool PictureAndTimingInfoPresent { get; }
        public bool Flag90khz { get; }
        public uint N { get; }
        public uint K { get; }
        public uint NumUnitsInTick { get; }
        public bool FixedFrameRateFlag { get; }
        public bool TemporalPocFlag { get; }
        public bool PictureToDisplayConversionFlag { get; }
        public AvcTimingAndHrdDescriptor_0x2A(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            HrdManagementValidFlag = (bytes[pointer] & 0x80) != 0;
            //reserved 6 bits
            PictureAndTimingInfoPresent = (bytes[pointer++] & 0x01) != 0;
            if (PictureAndTimingInfoPresent)
            {
                Flag90khz = (bytes[pointer] & 0x80) != 0;
                //reserved 7 bits
                if (Flag90khz)
                {
                    N = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                    pointer += 4;
                    K = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                }
                NumUnitsInTick = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                pointer += 4;
            }
            FixedFrameRateFlag = (bytes[pointer] & 0x80) != 0;
            TemporalPocFlag = (bytes[pointer] & 0x40) != 0;
            PictureToDisplayConversionFlag = (bytes[pointer] & 0x20) != 0;
            //reserved 5 bits
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Hrd Management Valid Flag: {HrdManagementValidFlag}, Picture And Timing Info Present: {PictureAndTimingInfoPresent}\n";
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Hrd Management Valid Flag: {HrdManagementValidFlag}, Picture And Timing Info Present: {PictureAndTimingInfoPresent}\n";
        }
    }
}
