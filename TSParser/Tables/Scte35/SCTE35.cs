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
        public byte ProtocolVersion { get; }
        public bool EncryptedPacket { get; }
        public byte EncryptionAlgorithm { get; }
        public ulong PtsAdjustment { get; }
        public byte CwIndex { get; }
        public ushort Tier { get; }
        public ushort SpliceCommandLength { get; }
        public byte SpliceCommandType { get; }
        public SpliceCommand SpliceCommand { get; } = null!;
        public ushort DescriptorLoopLength { get; }
        public List<Descriptor> SpliceDescriptors { get; } = null!;
        public uint ECRC32 { get; }
        public override ushort TablePid { get; }        
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
            //reserved 2bits
            SectionLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            ProtocolVersion = bytes[pointer++];
            EncryptedPacket = (bytes[pointer] & 0x80) != 0;
            EncryptionAlgorithm = (byte)((bytes[pointer] & 0x7E) >> 1);
            PtsAdjustment = ((BinaryPrimitives.ReadUInt64BigEndian(bytes[pointer..]) & 0x01FFFFFFFF000000) >> 24);
            pointer += 5;
            CwIndex = bytes[pointer++];
            Tier = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer++..]) >> 4);
            SpliceCommandLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            SpliceCommandType = bytes[pointer++];
            SpliceCommand = GetCommand(SpliceCommandType, bytes.Slice(pointer, SpliceCommandLength));

            if (SpliceCommandType == 0x00)
            {
                pointer += SpliceCommandLength - 1;
            }
            else
            {
                pointer += SpliceCommandLength;
            }

            DescriptorLoopLength = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            if (bytes.Length < pointer + DescriptorLoopLength)
            {
                Logger.Send(LogStatus.WARNING, $"SCTE35 pid: {TablePid}, Descriptor Loop Length is greate than table length, desc loop length: {DescriptorLoopLength}, pointer: {pointer}, bytes length: {bytes.Length}");
                CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]); // return with crc to prevent duplicates outgoing tables
                return;
            }
            var descAllocation = $"Table: SCTE35, table pid: {TablePid}";
            SpliceDescriptors = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, DescriptorLoopLength), descAllocation, TableId);
            pointer += DescriptorLoopLength;

            //alignment stuffing

            if (EncryptedPacket)
            {
                ECRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^8..]);
            }
            CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
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
            str += $"{prefix}Encrypted packet: {EncryptedPacket}\n";
            str += $"{prefix}Encryption Algorithm: {GetEncryptionAlgo(EncryptionAlgorithm)}\n";
            str += $"{prefix}Pts Adjustment: 0x{PtsAdjustment:X}\n";
            str += $"{prefix}Cw index: {CwIndex}\n";
            str += $"{prefix}Tier: 0x{Tier:X}\n";
            str += $"{prefix}Splice Command Length: {SpliceCommandLength}\n";
            str += $"{prefix}Splice Command Type: {Dictionaries.GetSliceCommandTypeName(SpliceCommandType)}\n";
            str += SpliceCommand.Print(prefixLen + 4);
            str += $"{prefix}Descriptor Loop Length: {DescriptorLoopLength}\n";
            if (DescriptorLoopLength > 0)
            {
                foreach (var desc in SpliceDescriptors)
                {
                    str += desc.Print(prefixLen + 4);
                }
            }
            if (EncryptedPacket)
            {
                str += $"{prefix}ECRC32: 0x{ECRC32:X}\n";
            }
            str += $"{prefix}SCTE35 CRC32: 0x{CRC32:X}\n";
            return str;
        }

        private string GetEncryptionAlgo(byte bt)
        {
            return bt switch
            {
                0 => "No encryption",
                1 => "DES ECB mode",
                2 => "DES CBC mode",
                3 => "Triple DES EDE3-ECB mode",
                byte n when n >= 4 && n <= 31 => "Reserved",
                _ => "User private",
            };
        }

        private SpliceCommand GetCommand(byte bt, ReadOnlySpan<byte> bytes)
        {
            try
            {
                switch (bt)
                {
                    case 0x00: return new SpliceNull(bytes);
                    case 0x04: return new SpliceSchedule(bytes);
                    case 0x05: return new SpliceInsert(bytes);
                    case 0x06: return new TimeSignal(bytes);
                    case 0x07: return new BandwidthReservation(bytes);
                    case 0xFF: return new PrivateCommand(bytes);
                    default:
                        {
                            Logger.Send(LogStatus.WARNING, $"Unknown Splice command type: 0x{bt:X} return base command");
                            return new SpliceCommand(bytes);
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.EXCEPTION, $"Ecxeption while deserelise splice command", ex);
                return new SpliceCommand(bytes);
            }

        }

    }
}
