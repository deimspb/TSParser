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

using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record AvcVideoDescriptor_0x28 : Descriptor
    {
        //TODO: implement beter with Rec. ITU-T H.264 | ISO/IEC 14496-10.
        public byte ProfileIdc { get; }
        public bool ConstraintSet0Flag { get; }
        public bool ConstraintSet1Flag { get; }
        public bool ConstraintSet2Flag { get; }
        public bool ConstraintSet3Flag { get; }
        public bool ConstraintSet4Flag { get; }
        public bool ConstraintSet5Flag { get; }
        public byte AvcCompatibleFlags { get; }
        public byte LevelIdc { get; }
        public bool AvcStillPresent { get; }
        public bool Avc24HourPictureFlag { get; }
        public bool FramePackingSeiNotPresentFlag { get; }
        public AvcVideoDescriptor_0x28(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ProfileIdc = bytes[pointer++];
            ConstraintSet0Flag = (bytes[pointer] & 0x80) != 0;
            ConstraintSet1Flag = (bytes[pointer] & 0x40) != 0;
            ConstraintSet2Flag = (bytes[pointer] & 0x20) != 0;
            ConstraintSet3Flag = (bytes[pointer] & 0x10) != 0;
            ConstraintSet4Flag = (bytes[pointer] & 0x08) != 0;
            ConstraintSet5Flag = (bytes[pointer] & 0x04) != 0;
            AvcCompatibleFlags = (byte)(bytes[pointer++] & 0x03);
            LevelIdc = bytes[pointer++];
            AvcStillPresent = (bytes[pointer] & 0x80) != 0;
            Avc24HourPictureFlag = (bytes[pointer] & 0x40) != 0;
            FramePackingSeiNotPresentFlag = (bytes[pointer] & 0x20) != 0;
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Profile Idc: {ProfileIdc}, Level Idc: {LevelIdc}\n";
        }
    }
}
