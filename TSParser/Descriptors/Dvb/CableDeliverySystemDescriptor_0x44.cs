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
using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record CableDeliverySystemDescriptor_0x44 : Descriptor
    {
        public uint Frequency { get; }
        public string FrequencyStr => $"{Frequency / 10} Khz";
        public byte FecOuter { get; }
        public string FecOuterStr=>GetFecOuter(FecOuter);
        public byte Modulation { get; }
        public string ModulationStr =>GetModulationStr(Modulation);
        public uint SymbolRate { get; }
        public string SymbolRateStr => $"{SymbolRate} Sym/sec";
        public byte FecInner { get; }
        public string FecInnerStr=>GetInnerFec(FecInner);
        public CableDeliverySystemDescriptor_0x44(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Frequency = Utils.BcdToUint(bytes.Slice(pointer, 4));
            pointer += 5;// reserved 12 bits
            FecOuter = (byte)(bytes[pointer++] & 0xF);
            Modulation = bytes[pointer++];
            SymbolRate = (uint)(((bytes[pointer] & 0x3f) >> 4) * 100000 + (bytes[pointer] & 0xf) * 10000 +
                          (bytes[pointer + 1] >> 4) * 1000 + (bytes[pointer + 1] & 0xf) * 100 +
                          (bytes[pointer + 2] >> 4) * 10 + (bytes[pointer + 2] & 0xf));
            pointer += 3;
            FecInner = (byte)(bytes[pointer] & 0xF);
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {FrequencyStr}, {FecInnerStr}, {ModulationStr}, {SymbolRateStr}, {FecInnerStr}";
        }
        private string GetFecOuter(byte bt)
        {
            switch (bt)
            {
                case 0: return "not defined";
                case 1: return "no outer FEC coding";
                case 2: return "RS(204/188)";
                    default: return "reserved for future use";
            }
        }
        private string GetModulationStr(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "not defined";
                case 0x01: return "16-QAM";
                case 0x02: return "32-QAM";
                case 0x03: return "64-QAM";
                case 0x04: return "128-QAM";
                case 0x05: return "256-QAM";
                default: return "reserved for future use";
            }
        }
        private string GetInnerFec(byte bt)
        {
            switch (bt)
            {
                case 0b0000: return "not defined";
                case 0b0001: return "1/2 conv. code rate";
                case 0b0010: return "2/3 conv. code rate";
                case 0b0011: return "3/4 conv. code rate";
                case 0b0100: return "5/6 conv. code rate";
                case 0b0101: return "7/8 conv. code rate";
                case 0b0110: return "8/9 conv. code rate";
                case 0b0111: return "3/5 conv. code rate";
                case 0b1000: return "4/5 conv. code rate";
                case 0b1001: return "9/10 conv. code rate";
                case byte n when n >= 0b1010 && n <= 0b1110: return "reserved for future use";
                case 0b1111: return "no conv. Coding";
                    default : return "Incorrect coding";
            }
        }

    }
}
