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

namespace TSParser.Descriptors.Dvb
{
    public record HevcVideoDescriptor_0x38 : Descriptor
    {
        public byte ProfileSpace { get; }
        public bool TierFlag { get; }
        public byte ProfileIdc { get; }
        public uint ProfileCompatibilityIndication { get; }
        public bool ProgressiveSourceFlag { get; }
        public bool InterlacedSourceFlag { get; }
        public bool NonPackedConstraintFlag { get; }
        public bool FrameOnlyConstraintFlag { get; }
        public ulong Copied44bits { get; }
        public byte LevelIdc { get; }
        public bool TemporalLayerSubsetFlag { get; }
        public bool HevcStillPresentFlag { get; }
        public bool Hevc24hrPicturePresentFlag { get; }
        public bool SubPicHrdParamsNotPresentFlag { get; }
        public byte HdrWcgIdc { get; }
        public byte TemporalIdMin { get; }
        public byte TemporalIdMax { get; }
        public HevcVideoDescriptor_0x38(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ProfileSpace = (byte)((bytes[pointer] & 0xC0) >> 6);
            TierFlag = (bytes[pointer]&0x20)!= 0;
            ProfileIdc = (byte)(bytes[pointer++] & 0x1F);
            ProfileCompatibilityIndication = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            ProgressiveSourceFlag = (bytes[pointer]&0x80)!= 0;
            InterlacedSourceFlag = (bytes[pointer]&0x40)!= 0;
            NonPackedConstraintFlag = (bytes[pointer] & 0x20) != 0;
            FrameOnlyConstraintFlag = (bytes[pointer]&0x10)!= 0;
            Copied44bits = (ulong)((BinaryPrimitives.ReadInt64BigEndian(bytes[pointer..])&0x0FFFFFFFFF000000)>>24);
            pointer += 6;
            LevelIdc = bytes[pointer++];
            TemporalLayerSubsetFlag = (bytes[pointer] & 0x80) != 0;
            HevcStillPresentFlag = (bytes[pointer] & 0x40) != 0;
            Hevc24hrPicturePresentFlag = (bytes[pointer] & 0x20) != 0;
            SubPicHrdParamsNotPresentFlag = (bytes[pointer] & 0x10) != 0;
            //reserved 2 bits
            HdrWcgIdc = (byte)(bytes[pointer++] & 0x3);
            if (TemporalLayerSubsetFlag)
            {
                TemporalIdMin = (byte)((bytes[pointer++] & 0xE0) >> 5);
                //reserved 5 bits
                TemporalIdMax = (byte)((bytes[pointer++] & 0xE0) >> 5);
            }
        }

        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Profile Idc: {ProfileIdc}, Profile Compatibility Indication: 0x{ProfileCompatibilityIndication:X}, Level Idc: {LevelIdc}, Hdr Wcg Idc: {HdrWcgIdc}\n";
        }
    }
}
