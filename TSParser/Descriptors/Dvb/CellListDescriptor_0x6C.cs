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
    public record CellListDescriptor_0x6C : Descriptor
    {
        public List<Cell> Cells { get; } = null!;
        public CellListDescriptor_0x6C(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Cells = new List<Cell>();
            while(pointer< DescriptorLength - 2)
            {
                var cell = new Cell(bytes[pointer..]);
                pointer += cell.SubcellInfoLoopLength + 10;
                Cells.Add(cell);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";

            foreach(Cell cell in Cells)
            {
                str+=cell.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct Cell
    {
        public ushort CellId { get; }
        public short CellLatitude { get; }
        public short CellLongitude { get; }
        public short CellExtentOfLatitude { get; }
        public short CellExtentOfLongitude { get; }
        public byte SubcellInfoLoopLength { get; }
        public SubCell[] SubCells { get; } = null!;
        public Cell(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            CellId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            CellLatitude = BinaryPrimitives.ReadInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            CellLongitude = BinaryPrimitives.ReadInt16BigEndian (bytes[pointer..]);
            pointer += 2;
            CellExtentOfLatitude = (short)(BinaryPrimitives.ReadInt16BigEndian(bytes[pointer..]) >> 4);
            pointer++;
            CellExtentOfLongitude = (short)(BinaryPrimitives.ReadInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer++;
            SubcellInfoLoopLength = bytes[pointer++];
            if (SubcellInfoLoopLength > 0)
            {
                SubCells = new SubCell[SubcellInfoLoopLength / 8];
                for (int i = 0; i < SubCells.Length; i++)
                {
                    SubCells[i] = new SubCell(bytes.Slice(pointer, 8));
                    pointer += 8;
                }
            }
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix} Cell id: {CellId}\n";
            str += $"{prefix}Cell Latitude: {CellLatitude}\n";
            str += $"{prefix}Cell Longitude: {CellLongitude}\n";
            str += $"{prefix}Cell Extent Of Latitude: {CellExtentOfLatitude}\n";
            str += $"{prefix}Cell Extent Of Longitude: {CellExtentOfLongitude}\n";
            str += $"{prefix}Subcell Info Loop Length: {SubcellInfoLoopLength}\n";
            if (SubcellInfoLoopLength > 0)
            {
                foreach(var item in SubCells)
                {
                    str += item.Print(prefixLen + 4);
                }
            }
            return str;
        }

    }
    public struct SubCell
    {
        public byte CellIdExtension { get; }
        public short SubcellLatitude { get; }//TODO: implement with coordinates
        public short SubcellLongitude { get; }
        public short SubcellExtentOfLatitude { get; }
        public short SubcellExtentOfLongitude { get; }
        public SubCell(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            CellIdExtension = bytes[pointer++];
            SubcellLatitude = BinaryPrimitives.ReadInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            SubcellLongitude = BinaryPrimitives.ReadInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            SubcellExtentOfLatitude = (short)(BinaryPrimitives.ReadInt16BigEndian(bytes[pointer..]) >> 4);
            pointer++;
            SubcellExtentOfLongitude = (short)(BinaryPrimitives.ReadInt16BigEndian(bytes[pointer..]) & 0x0FFF);
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str= $"{headerPrefix} Cell id extension: {CellIdExtension}\n";
            str += $"{prefix}Subcell Latitude: {SubcellLatitude}\n";
            str += $"{prefix}Subcell Longitude: {SubcellLongitude}\n";
            str += $"{prefix}Subcell Extent Of Latitude: {SubcellExtentOfLatitude}\n";
            str += $"{prefix}Subcell Extent Of Longitude: {SubcellExtentOfLongitude}\n";
            return str;
        }
    }
}
