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
    public record SatelliteDeliverySystemDescriptor_0x43 : Descriptor
    {
        public uint Frequency { get; }
        public string FrequencyStr => $"{Frequency / 100} Mhz";
        public ushort OrbitalPosition { get; }
        public string OrbitalPositionStr => $"{OrbitalPosition / 10} deg";
        public bool WestEastFlag { get; }
        public string WestEastStr => WestEastFlag ? "East" : "West";
        public byte Polarization { get; }
        public string PolarizationStr => GetPolarisationStr(Polarization);
        public byte RollOff { get; }
        public string RollOffStr => GetRollOffString(RollOff);
        public bool ModulationSystem { get; }
        public string ModualtionSystemStr => ModulationSystem ? "DVB-S2" : "DVB-S";
        public byte ModulationType { get; }
        public string ModulationTypeStr => GetModulationTypeStr(ModulationType);
        public uint SymbolRate { get; }
        public string SymbolRateStr => $"{SymbolRate} Sym/sec";
        public byte FecInner { get; }
        public string FecInnerStr => GetFecInnerStr(FecInner);
        public SatelliteDeliverySystemDescriptor_0x43(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Frequency = Utils.BcdToUint(bytes.Slice(pointer, 4));
            pointer += 4;
            OrbitalPosition = (ushort)((bytes[pointer] >> 4) * 1000 + (bytes[pointer] & 0xf) * 100 + (bytes[pointer + 1] >> 4) * 10 + (bytes[pointer + 1] & 0xf));
            pointer += 2;
            WestEastFlag = (bytes[pointer] & 0x80) != 0;
            Polarization = (byte)((bytes[pointer] & 0x60) >> 5);
            RollOff = (byte)((bytes[pointer] & 0x18) >> 3);
            ModulationSystem = (bytes[pointer] & 0x4) != 0;
            ModulationType = (byte)(bytes[pointer++] & 0x3);
            SymbolRate = (uint)(((bytes[pointer] & 0x3f) >> 4) * 100000 + (bytes[pointer] & 0xf) * 10000 +
                          (bytes[pointer + 1] >> 4) * 1000 + (bytes[pointer + 1] & 0xf) * 100 +
                          (bytes[pointer + 2] >> 4) * 10 + (bytes[pointer + 2] & 0xf));
            pointer += 3;
            FecInner = (byte)(bytes[pointer] & 0xF);
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {FrequencyStr}, {SymbolRateStr} {OrbitalPositionStr}, {WestEastStr}, {PolarizationStr}, {ModualtionSystemStr}, {RollOffStr}, {ModulationTypeStr}, {FecInnerStr}\n";
        }
        private string GetPolarisationStr(byte polarization)
        {
            switch (polarization)
            {
                case 0x00: return "Linear - horizontal";
                case 0x01: return "Linear - vertical";
                case 0x02: return "Circular - left";
                case 0x03: return "Circular - right";
                default:
                    {
                        throw new Exception("Incorrect polarization!");
                    }
            }
        }
        private string GetRollOffString(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "a = 0,35";
                case 0x01: return "a = 0,25";
                case 0x02: return "a = 0,20";
                case 0x03: return "reserved";
                default:
                    {
                        throw new Exception("Incorrect RollOff!");
                    }
            }
        }
        private string GetModulationTypeStr(byte md)
        {
            switch (md)
            {
                case 0x0:
                    return "Auto";
                case 0x01:
                    return "QPSK";
                case 0x02:
                    return "8PSK";
                case 0x03:
                    return "16-QAM (n/a for DVB-S2)";
                default:
                    {
                        throw new Exception("Incorrect modulation type!");
                    }

            }

        }
        private string GetFecInnerStr(byte fecInner)
        {
            switch (fecInner)
            {
                case 0x0:
                    return "not defined";
                case 0x1:
                    return "1/2";
                case 0x2:
                    return "2/3";
                case 0x3:
                    return "3/4";
                case 0x4:
                    return "5/6";
                case 0x5:
                    return "7/8";
                case 0x6:
                    return "8/9";
                case 0x7:
                    return "3/5";
                case 0x8:
                    return "4/5";
                case 0x9:
                    return "9/10";
                case 0xf:
                    return "no conv. coding";
                default:
                    return "reserved";
            }
        }
    }
}
