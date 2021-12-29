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
    public record CellFrequencyLinkDescriptor_0x6D : Descriptor
    {
        public List<CellItem> Cells { get; } = null!;
        public CellFrequencyLinkDescriptor_0x6D(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Cells =new List<CellItem>();
            while(pointer < DescriptorLength - 2)
            {
                var cell = new CellItem(bytes[pointer..]);
                pointer += cell.SubcellInfoLoopLength + 6;
                Cells.Add(cell);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";

            foreach(var cell in Cells)
            {
                str+=cell.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct CellItem
    {
        public ushort CellId { get; }
        public uint Frequency { get; }
        public byte SubcellInfoLoopLength { get; }
        public SubCell[] SubCells { get; } = null!;
        public CellItem(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            CellId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            Frequency = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]) * 10;
            pointer += 4;
            SubcellInfoLoopLength = bytes[pointer++];
            if(SubcellInfoLoopLength > 0)
            {
                SubCells = new SubCell[SubcellInfoLoopLength / 5];
                for(int i = 0; i < SubCells.Length; i++)
                {
                    SubCells[i] = new SubCell(bytes.Slice(pointer, 5));
                    pointer += 5;
                }
            }           

        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Cell id: {CellId}\n";
            str += $"{prefix}Frequency: {Frequency} Hz\n";
            str += $"{prefix}Subcell Info Loop Length: {SubcellInfoLoopLength}\n";
            if(SubcellInfoLoopLength > 0)
            {
                foreach(SubCell subCell in SubCells)
                {
                    str+=subCell.Print(prefixLen + 4);
                }
            }
            return str;
        }
    }
    public struct SubCellItem//5 bytes
    {
        public byte CellIdExtension { get; }
        public uint TransposerFrequency { get; }
        public SubCellItem(ReadOnlySpan<byte> bytes)
        {
            CellIdExtension = bytes[0];
            TransposerFrequency = BinaryPrimitives.ReadUInt32BigEndian(bytes[1..]);
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix} Cell id extension: {CellIdExtension}, Transposer Frequency: {TransposerFrequency}\n";
        }
    }
}
