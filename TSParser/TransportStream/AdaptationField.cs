// Copyright 2021 Eldar Nizamutdinov 
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

namespace TSParser.TransportStream
{
    public readonly struct AdaptationField
    {
        public byte AdaptationFieldLength { get; } = default;
        public bool DiscontinuityIndicator { get; } = default;
        public bool RandomAccessIndicator { get; } = default;
        public bool ElementaryStreamPriorityIndicator { get; } = default;
        public bool PCRFlag { get; } = default;
        public bool OPCRFlag { get; } = default;
        public bool SplicingPointFlag { get; } = default;
        public bool TransportPrivateDataFlag { get; } = default;
        public bool AdaptationFieldExtensionFlag { get; } = default;
        public ulong ProgramClockReferenceBase { get; } = default;
        public ushort ProgramClockReferenceExtension { get; } = default;
        public ulong OriginalProgramClockReferenceBase { get; } = default;
        public ushort OriginalProgramClockReferenceExtension { get; } = default;
        public byte SpliceCountdown { get; } = default;
        public byte TransportPrivateDataLength { get; } = default;
        public byte[] PrivateDataByte { get; } = Array.Empty<byte>();
        public byte AdaptationFieldExtensionLength { get; } = default;
        public bool LtwFlag { get; } = default;
        public bool PiecewiseRateFlag { get; } = default;
        public bool SeamlessSpliceFlag { get; } = default;
        public bool LtwValidFlag { get; } = default;
        public ushort LtwOffset { get; } = default;
        public uint PiecewiseRate { get; } = default;
        public byte SpliceType { get; } = default;
        public ulong DTSNext_AU { get; } = default;
        public ulong PcrValue { get; } = default;
        public TimeSpan PcrTime => Utils.GetPcrTimeSpan(PcrValue);
        public ulong OPcrValue { get; } = default;
        public TimeSpan OPcrTime => Utils.GetPcrTimeSpan(OPcrValue);

        public AdaptationField(ReadOnlySpan<byte> bytes, out int pointer)
        {
            pointer = 0;
            AdaptationFieldLength = bytes[pointer++];
            if (AdaptationFieldLength > 0)
            {
                DiscontinuityIndicator = (bytes[pointer] & 0x80) != 0;
                RandomAccessIndicator = (bytes[pointer] & 0x40) != 0;
                ElementaryStreamPriorityIndicator = (bytes[pointer] & 0x20) != 0;
                PCRFlag = (bytes[pointer] & 0x10) != 0;
                OPCRFlag = (bytes[pointer] & 0x8) != 0;
                SplicingPointFlag = (bytes[pointer] & 0x4) != 0;
                TransportPrivateDataFlag = (bytes[pointer] & 0x2) != 0;
                AdaptationFieldExtensionFlag = (bytes[pointer++] & 0x1) != 0;

                if (PCRFlag)
                {
                    ProgramClockReferenceBase = Utils.GetPcrBase(bytes.Slice(pointer, 6));
                    // reserved 6 bits
                    pointer += 4;
                    ProgramClockReferenceExtension = Utils.GetPcrExtension(bytes.Slice(pointer, 2));
                    pointer += 2;
                    PcrValue = ProgramClockReferenceBase * 300 + ProgramClockReferenceExtension;
                }

                if (OPCRFlag)
                {
                    OriginalProgramClockReferenceBase = Utils.GetPcrBase(bytes.Slice(pointer, 6));
                    // reserved 6 bits
                    pointer += 4;
                    OriginalProgramClockReferenceExtension = Utils.GetPcrExtension(bytes.Slice(pointer, 2));
                    pointer += 2;
                    OPcrValue = OriginalProgramClockReferenceBase * 300 + OriginalProgramClockReferenceExtension;
                }

                if (SplicingPointFlag)
                {
                    SpliceCountdown = bytes[pointer++];
                }

                if (TransportPrivateDataFlag)
                {
                    TransportPrivateDataLength = bytes[pointer++];
                    PrivateDataByte = new byte[TransportPrivateDataLength];
                    for (int i = 0; i < TransportPrivateDataLength; i++)
                    {
                        PrivateDataByte[i] = bytes[pointer++]; //TODO: refactor this
                    }

                }

                if (AdaptationFieldExtensionFlag)
                {
                    AdaptationFieldExtensionLength = bytes[pointer++];
                    LtwFlag = (bytes[pointer] & 0x80) != 0;
                    PiecewiseRateFlag = (bytes[pointer] & 0x40) != 0;
                    SeamlessSpliceFlag = (bytes[pointer++] & 0x20) != 0;
                    //reserved 5 bits
                    if (LtwFlag)
                    {
                        LtwValidFlag = (bytes[pointer] & 0x80) != 0;
                        LtwOffset = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x7FFF);
                        pointer += 2;
                    }

                    if (PiecewiseRateFlag)
                    {
                        //reserved 2 bits
                        PiecewiseRate = (uint)((BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]) & 0x3FFF00) >> 8);//TODO: check this
                        pointer += 3;
                    }

                    if (SeamlessSpliceFlag)
                    {
                        SpliceType = (byte)((bytes[pointer] & 0xF0) >> 4);
                        DTSNext_AU = Utils.GetPtsDts(bytes.Slice(pointer, 5));
                        pointer += 5;
                    }
                }
            }

            pointer = AdaptationFieldLength + 1;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Adaptation field\n";
            str += $"{prefix}Discontinuity Indicator: {DiscontinuityIndicator}\n";
            str += $"{prefix}Random Access Indicator: {RandomAccessIndicator}\n";
            str += $"{prefix}Elementary Stream Priority Indicator: {ElementaryStreamPriorityIndicator}\n";

            return str;

        }
    }
}
