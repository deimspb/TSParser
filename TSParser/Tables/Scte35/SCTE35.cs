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
using TSParser.Descriptors;
using TSParser.DictionariesData;
using TSParser.Service;
using TSParser.Tables.Scte35;

namespace TSParser.Tables.DvbTables
{
    public record SCTE35 : Table
    {
        public bool PrivateIndicator { get; }
        public bool IsEncryptedPacket { get; }


        public ushort SpliceCommandLength { get; }

        public ushort DescriptorLoopLength { get; }

        public uint ECRC32 { get; }
        public override ushort TablePid { get; }
        public byte[] AlignmentStuffing { get; }

        #region SpliceInfoSectionType

        public byte SapType { get; }
        public byte ProtocolVersion { get; }
        public EncryptedPacket EncryptedPacket { get; }
        public ulong PtsAdjustment { get; }
        public ushort Tier { get; }
        public SpliceCommand SpliceCommandItem { get; } = null!;
        public List<Descriptor> SpliceDescriptorItems { get; } = null!;
        public byte SpliceCommandType { get; }

        #endregion
        public SCTE35(ReadOnlySpan<byte> bytes, ushort scte35Pid) : this(bytes)
        {
            TablePid = scte35Pid;
        }
        public SCTE35(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            TableId = bytes[pointer++];

            if (TableId != 0xFC)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {TableId} for SCTE35 table");
                return;
            }

            SectionSyntaxIndicator = (bytes[pointer] & 0x80) != 0;
            PrivateIndicator = (bytes[pointer] & 0x40) != 0;
            SapType = (byte)((bytes[pointer] & 0x30) >> 4);
            SectionLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            ProtocolVersion = bytes[pointer++];
            IsEncryptedPacket = (bytes[pointer] & 0x80) != 0;
            EncryptedPacket = new EncryptedPacket(bytes[pointer..]);
            PtsAdjustment = ((BinaryPrimitives.ReadUInt64BigEndian(bytes[pointer..]) & 0x01FFFFFFFF000000) >> 24);
            pointer += 6;
            Tier = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer++..]) >> 4);
            SpliceCommandLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            SpliceCommandType = bytes[pointer++];

            if (SpliceCommandLength == 0xFFF)
            {
                SpliceCommandItem = GetCommand(bytes[pointer..], SpliceCommandType);
                pointer += SpliceCommandItem.SpliceCommandLength;
            }
            else
            {
                SpliceCommandItem = GetCommand(bytes.Slice(pointer, SpliceCommandLength), SpliceCommandType);
                pointer += SpliceCommandLength;
            }
            DescriptorLoopLength = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            SpliceDescriptorItems = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, DescriptorLoopLength), "SCTE 35", TableId);
            var stuffedBytes = 0;

            if (IsEncryptedPacket)
            {
                ECRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^8..]);
                stuffedBytes = bytes.Length - pointer - 8;
            }

            CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
            stuffedBytes = bytes.Length - pointer - 4;

            if (stuffedBytes > 0)
            {
                AlignmentStuffing = bytes.Slice(pointer, stuffedBytes).ToArray();
            }

        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}-=SCTE35 pid: {TablePid}=-\n";

            str += $"{prefix}Section syntax indicator: {SectionSyntaxIndicator}\n";
            str += $"{prefix}Private indicator: {PrivateIndicator}\n";
            str += $"{prefix}Section length: {SectionLength}\n";
            str += $"{prefix}Protocol version: {ProtocolVersion}\n";
            str += $"{prefix}Pts Adjustment: 0x{PtsAdjustment:X}\n";

            str += EncryptedPacket.Print(prefixLen + 4);

            str += $"{prefix}Tier: 0x{Tier:X}\n";
            str += $"{prefix}Splice Command Length: {SpliceCommandLength}\n";
            str += $"{prefix}Splice Command Type: {Dictionaries.GetSliceCommandTypeName(SpliceCommandType)}\n";
            str += SpliceCommandItem.Print(prefixLen + 4);
            str += $"{prefix}Descriptor Loop Length: {DescriptorLoopLength}\n";
            if (DescriptorLoopLength > 0)
            {
                foreach (var desc in SpliceDescriptorItems)
                {
                    str += desc.Print(prefixLen + 4);
                }
            }
            if (IsEncryptedPacket)
            {
                str += $"{prefix}ECRC32: 0x{ECRC32:X}\n";
            }
            str += $"{prefix}SCTE35 CRC32: 0x{CRC32:X}\n";
            return str;
        }



        private static SpliceCommand GetCommand(ReadOnlySpan<byte> bytes, byte bt)
        {
            try
            {
                switch (bt)
                {
                    case 0x00: return new SpliceNull(bytes, bt);
                    case 0x04: return new SpliceSchedule(bytes, bt);
                    case 0x05: return new SpliceInsert(bytes, bt);
                    case 0x06: return new TimeSignal(bytes, bt);
                    case 0x07: return new BandwidthReservation(bytes, bt);
                    case 0xFF: return new PrivateCommand(bytes, bt);
                    default:
                        {
                            Logger.Send(LogStatus.WARNING, $"Unknown Splice command type: 0x{bt:X} return base command");
                            return new SpliceCommand(bytes, bt);
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.EXCEPTION, $"Ecxeption while deserelise splice command", ex);
                return new SpliceCommand(bytes, bt);
            }

        }

    }
}
