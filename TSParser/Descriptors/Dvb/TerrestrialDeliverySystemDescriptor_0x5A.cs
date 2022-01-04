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
    public record TerrestrialDeliverySystemDescriptor_0x5A : Descriptor
    {
        public uint CentreFrequency { get; }
        public byte Bandwidth { get; }
        public bool Priority { get; }
        public bool TimeSlicingIndicator { get; }
        public bool MpeFecIndicator { get; }
        public byte Constellation { get; }
        public byte HierarchyInformation { get; }
        public byte CodeRateHpStream { get; }
        public byte CodeRateLpStream { get; }
        public byte GuardInterval { get; }
        public byte TransmissionMode { get; }
        public bool OtherFrequencyFlag { get; }
        public TerrestrialDeliverySystemDescriptor_0x5A(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            CentreFrequency = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..])*10;
            pointer += 4;
            Bandwidth = (byte)(bytes[pointer]>>5);
            Priority = (bytes[pointer] & 0x10) != 0;
            TimeSlicingIndicator = (bytes[pointer] & 0x08) != 0;
            MpeFecIndicator = (bytes[pointer++] & 0x04) != 0;
            //reserved 2 bits
            Constellation = (byte)(bytes[pointer] >> 6);
            HierarchyInformation = (byte)((bytes[pointer]&0x38)>>3);
            CodeRateHpStream = (byte)(bytes[pointer++] & 0x07);
            CodeRateLpStream = (byte)(bytes[pointer] >> 5);
            GuardInterval = (byte)((bytes[pointer] &0x18)>>3);
            TransmissionMode = (byte)((bytes[pointer] & 0x06)>>1);
            OtherFrequencyFlag = (bytes[pointer] & 0x01) != 0;
            //reserved 32 bit
        }       
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Centre Frequency:{CentreFrequency} Hz\n";
            str += $"{prefix}Bandwidth: {GetBw(Bandwidth)}\n";
            str += $"{prefix}Priority: {Priority}\n";
            str += $"{prefix}TimeSlicing Indicator: {TimeSlicingIndicator}\n";
            str += $"{prefix}MpeFec Indicator: {MpeFecIndicator}\n";
            str += $"{prefix}Constellation: {GetConstellation(Constellation)}\n";
            str += $"{prefix}Hierarchy Information: {GetConstellation(Constellation)}\n";
            str += $"{prefix}Code Rate HpStream: {GetCodeRateHpStream(CodeRateHpStream)}\n";
            str += $"{prefix}Code Rate LpStream: {GetCodeRateHpStream(CodeRateLpStream)}\n";
            str += $"{prefix}Guard Interval: {GetGuardInterval(GuardInterval)}\n";
            str += $"{prefix}Transmission Mode: {GetTransmissionMode(TransmissionMode)}\n";
            str += $"{prefix}Other Frequency Flag: {OtherFrequencyFlag}\n";
            return str;
        }
        private string GetBw(byte bt)
        {
            switch (bt)
            {
                case 0b000: return "8 MHz";
                case 0b001: return "7 MHz";
                case 0b010: return "6 MHz";
                case 0b011: return "5 MHz";
                default: return "Reserved for future use";
            }
        }
        private string GetConstellation(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "QPSK";
                case 0b01: return "16-QAM";
                case 0b10: return "64-QAM";
                default: return "reserved for future use";
            }
        }
        private string GetHierarchyInformation(byte bt)
        {
            switch (bt)
            {
                case 0b000: return "non-hierarchical, native interleaver";
                case 0b001: return "α = 1, native interleaver";
                case 0b010: return "α = 2, native interleaver";
                case 0b011: return "α = 4, native interleaver";
                case 0b100: return "non-hierarchical, in-depth interleaver";
                case 0b101: return "α = 1, in-depth interleaver";
                case 0b110: return "α = 2, in-depth interleaver";
                case 0b111: return "α = 4, in-depth interleaver";
                    default : return "unknown";
            }
        }
        private string GetCodeRateHpStream(byte bt)
        {
            switch (bt)
            {
                case 0b000: return "1/2";
                case 0b001: return "2/3";
                case 0b010: return "3/4";
                case 0b011: return "5/6";
                case 0b100: return "7/8";
                default: return "reserved for future use";
            }
        }
        private string GetGuardInterval(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "1/32";
                case 0b01: return "1/16";
                case 0b10: return "1/8";
                case 0b11: return "1/4";
                default: return "unknown";
            }
        }
        private string GetTransmissionMode(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "2k mode";
                case 0b01: return "8k mode";
                case 0b10: return "4k mode";
                default: return "reserved for future use";

            }
        }
    }
}
