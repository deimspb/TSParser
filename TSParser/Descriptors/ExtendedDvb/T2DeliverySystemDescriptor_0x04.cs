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

namespace TSParser.Descriptors.ExtendedDvb
{
    public record T2DeliverySystemDescriptor_0x04 : ExtendedDescriptor
    {
        public byte PlpId { get; }
        public ushort T2SystemId { get; }
        public byte Siso_Miso { get; }
        public byte Bandwidth { get; }
        public byte GuardInterval { get; }
        public byte TransmissionMode { get; }
        public bool OtherFrequencyFlag { get; }
        public bool TfsFlag { get; }
        public List<CellFrq> CellFrqs { get; } = null!;
        public T2DeliverySystemDescriptor_0x04(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 3;
            PlpId = bytes[pointer++];
            T2SystemId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            if (DescriptorLength > 4)
            {
                Siso_Miso = (byte)(bytes[pointer] >> 6);
                Bandwidth = (byte)((bytes[pointer++] & 0x3C) >> 2);
                GuardInterval = (byte)(bytes[pointer] >> 5);
                TransmissionMode = (byte)((bytes[pointer]&0x1C)>> 2);
                OtherFrequencyFlag = (bytes[pointer] & 0x02) != 0;
                TfsFlag = (bytes[pointer++] & 0x01) != 0;

                CellFrqs = new List<CellFrq>();
                while (pointer < DescriptorLength - 2)
                {
                    var cellFrq = new CellFrq(bytes[pointer..], TfsFlag);

                    if (TfsFlag)
                    {
                        pointer += cellFrq.FrequencyLoopLength + 2 + cellFrq.SubcellInfoLoopLength;
                    }
                    else
                    {
                        pointer += 7 + cellFrq.SubcellInfoLoopLength;
                    }

                    CellFrqs.Add(cellFrq);
                }

            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Extension tag: {DescriptorTagExtension}, {ExtensionDescriptorName}\n";
            str += $"{prefix}Plp Id: {PlpId}\n";
            str += $"{prefix}T2 SystemId: {T2SystemId}\n";
            str += $"{prefix}{GetSisoMiso(Siso_Miso)}\n";
            str += $"{prefix}Bandwidth: {GetBw(Bandwidth)}\n";
            str += $"{prefix}Guard Interval: {GetGuardInterval(GuardInterval)}\n";
            str += $"{prefix}Transmission Mode: {GetTrMode(TransmissionMode)}\n";
            str += $"{prefix}Other Frequency Flag: {OtherFrequencyFlag}\n";
            str += $"{prefix}Tfs Flag: {TfsFlag}\n";

            if(CellFrqs.Count > 0)
            {
                foreach(var cellFrq in CellFrqs)
                {
                    str+=cellFrq.Print(prefixLen + 4);
                }
            }

            return str;
        }
        private string GetSisoMiso(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "SISO";
                case 0b01: return "MISO";
                default: return "reserved for future use";
            }
        }
        private string GetBw(byte bt)
        {
            switch (bt)
            {
                case 0b0000: return "8 MHz";
                case 0b0001: return "7 MHz";
                case 0b0010: return "6 MHz";
                case 0b0011: return "5 MHz";
                case 0b0100: return "10 MHz";
                case 0b0101: return "1,712 MHz";
                    default : return "reserved for future use";
            }
        }
        private string GetGuardInterval(byte bt)
        {
            switch (bt)
            {
                case 0b000: return "1/32";
                case 0b001: return "1/16";
                case 0b010: return "1/8";
                case 0b011: return "1/4";
                case 0b100: return "1/128";
                case 0b101: return "19/128";
                case 0b110: return "19/256";
                default: return "reserved for future use";
            }
        }
        private string GetTrMode(byte bt)
        {
            switch (bt)
            {
                case 0b000: return "2k mode";
                case 0b001: return "8k mode";
                case 0b010: return "4k mode";
                case 0b011: return "1k mode";
                case 0b100: return "16k mode";
                case 0b101: return "32k mode";
                default: return "reserved for future use";
            }
        }
    }
    public struct CellFrq
    {
        public ushort CellId { get; }
        public byte FrequencyLoopLength { get; }
        public uint[] CentreFrequences { get; } = null!;
        public byte SubcellInfoLoopLength { get; }
        public SubCellFrq[] SubCellFrqs { get; } = null!;
        public CellFrq(ReadOnlySpan<byte> bytes,bool TfsFlag)
        {
            var pointer = 0;
            CellId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            if (TfsFlag)
            {
                FrequencyLoopLength = bytes[pointer++];
                CentreFrequences = new uint[FrequencyLoopLength / 4];
                for (int i = 0;CentreFrequences.Length > 0; i++)
                {
                    CentreFrequences[i] = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                    pointer += 4;
                }
            }
            else
            {
                CentreFrequences = new uint[1];
                FrequencyLoopLength = 0;
                CentreFrequences[0]=BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
                pointer += 4;
            }
            SubcellInfoLoopLength = bytes[pointer++];

            if (SubcellInfoLoopLength > 0)
            {
                SubCellFrqs = new SubCellFrq[SubcellInfoLoopLength / 5];
                for (int i = 0; i< SubCellFrqs.Length; i++)
                {
                    SubCellFrqs[i] = new SubCellFrq(bytes[pointer..]);
                    pointer += 5;
                }
            }
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Cell id: {CellId}\n";
            
            foreach(var frq in CentreFrequences)
            {
                str += $"{prefix}Frequency: {frq} Hz\n";
            }

            if(SubcellInfoLoopLength > 0)
            {
                str += $"{prefix}Subcell Info Loop Length: {SubcellInfoLoopLength}\n";
                foreach(var item in SubCellFrqs)
                {
                    str += item.Print(prefixLen + 4);
                }
            }
            return str;
        }
    }
    public struct SubCellFrq
    {
        public byte CellIdExtension { get; }
        public uint TransposerFrequency { get; }
        public SubCellFrq(ReadOnlySpan<byte> bytes)
        {
            CellIdExtension = bytes[0];
            TransposerFrequency = BinaryPrimitives.ReadUInt32BigEndian(bytes[1..]);
        }
        public string Print(int prefixLen) // 5 bytes
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Cell id extension: {CellIdExtension}, Transposer Frequency: {TransposerFrequency}\n";
        }
    }
}
