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

namespace TSParser.Tables.Mip
{
    public record MIP : Table
    {
        public byte SynchronizationId { get; }
        public ushort Pointer { get; }
        public bool PeriodicFlag { get; }
        public uint SynchronizationTimeStamp { get; }
        public uint MaximumDelay { get; }        
        public TpsMip TpsMip { get; }
        public byte IndividualAddressingLength { get; }
        public List<TxIdFunction> TxIdFunctions { get; } = null!;
        public MIP(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            SynchronizationId = bytes[pointer++];
            SectionLength = bytes[pointer++];
            Pointer = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            PeriodicFlag = (bytes[pointer] & 0x80) != 0;
            pointer += 2;
            //reserved 15 bits
            SynchronizationTimeStamp = (uint)(bytes[pointer++] << 16 | bytes[pointer++] << 8 | bytes[pointer++]);
            MaximumDelay = (uint)(bytes[pointer++]<<16|bytes[pointer++]<<8|bytes[pointer++]);
            TpsMip =  new(BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]));
            pointer += 4;
            IndividualAddressingLength = bytes[pointer++];
            if (IndividualAddressingLength > 0)
            {
                var loopEnd = pointer + IndividualAddressingLength;
                TxIdFunctions = new List<TxIdFunction>();
                while(pointer < loopEnd)
                {
                    var txFunc = new TxIdFunction(bytes[pointer..]);
                    pointer += txFunc.FunctionLoopLength + 3;
                    TxIdFunctions.Add(txFunc);
                }               
            }
            CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}-=MIP=-\n";
            str += $"{prefix}Synchronization Id: {SynchronizationId}\n";
            str += $"{prefix}Section length: {SectionLength}\n";
            str += $"{prefix}Pointer: {Pointer}\n";
            str += $"{prefix}Periodic flag: {PeriodicFlag}\n";
            str += $"{prefix}Synchronization TimeStamp: {(double)SynchronizationTimeStamp / 10000} ms\n";
            str += $"{prefix}Maximum Delay: {(double)MaximumDelay / 10000} ms\n";
            str += TpsMip.Print(prefixLen + 4);
            str += $"{prefix}Individual Addressing Length: {IndividualAddressingLength}\n";
            if (IndividualAddressingLength > 0)
            {
                foreach(var item in TxIdFunctions)
                {
                    str+=item.Print(prefixLen + 4);
                }
            }
            str += $"{prefix}MIP CRC32: 0x{CRC32:X}\n";
            return str;
        }
        public virtual bool Equals(MIP? table)
        {
            if (table == null) return false;

            return CRC32 == table.CRC32;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return (int)CRC32;
            }
        }
    }
    public struct TpsMip
    {
        public byte Constellation { get; }
        public byte Hierarchy { get; }
        public byte CodeRate { get; }
        public byte GuardInterval { get; }
        public byte TransmissionMode { get; }
        public byte DvbhSignalling { get; }
        public byte Bandwidth { get; }
        public bool TsPriority { get; }
        public TpsMip(uint tpsMip)
        {
            Constellation = (byte)(tpsMip >> 30);
            Hierarchy = (byte)((tpsMip >> 27) & 0x07);
            CodeRate = (byte)((tpsMip >> 24) & 0x07);            
            GuardInterval = (byte)((tpsMip & 0x00C00000)>>22);
            TransmissionMode = (byte)((tpsMip & 0x00300000) >> 20);
            Bandwidth = (byte)((tpsMip & 0x000C0000) >> 18);
            TsPriority = (tpsMip & 0x00020000) != 0;
            DvbhSignalling = (byte)((tpsMip & 0x00018000) >> 15);
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Tps MIP:\n";
            str += $"{prefix}Constellation: {GetConstelation(Constellation)}\n";
            str += $"{prefix}Hierarchy: {GetHierarchy(Hierarchy)}\n";
            str += $"{prefix}Code Rate: {GetCodeRate(CodeRate)}\n";
            str += $"{prefix}Guard Interval: {GetGuardInterval(GuardInterval)}\n";
            str += $"{prefix}Transmission Mode: {GetTransmissionMode(TransmissionMode)}\n";
            str += $"{prefix}Dvb-H Signalling: {GetDvbHSignaling(DvbhSignalling)}\n";
            str += $"{prefix}Bandwidth: {GetBw(Bandwidth)}\n";
            str += $"{prefix}TsPriority: {GetPriority(TsPriority)}\n";
            return str;
        }
        private string GetConstelation(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "QPSK";
                case 0b01: return "16-QAM";
                case 0b10: return "64-QAM";
                default: return "Reserved";
            }
        }
        private string GetHierarchy(byte bt)
        {
            switch (bt)
            {
                case 0b000: return "Non hierarchical";
                case 0b001: return "α = 1";
                case 0b010: return "α = 2";
                case 0b011: return "α = 4";
                default : return "Not implement";
            }
        }
        private string GetCodeRate(byte bt)
        {
            switch (bt)
            {
                case 0b000: return "1/2";
                case 0b001: return "2/3";
                case 0b010: return "3/4";
                case 0b011: return "5/6";
                case 0b100: return "7/8";
                default: return "Reserved";
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
                default: return "Unknown";
            }
        }
        private string GetTransmissionMode(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "2k mode";
                case 0b01: return "8k mode";
                case 0b10: return "4k mode";
                default: return "reserved";
            }
        }
        private string GetDvbHSignaling(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "Time Slicing is not used, MPE-FEC not used";
                case 0b01: return "Time Slicing is not used, At least one elementary stream uses MPE-FEC";
                case 0b10: return "At least one elementary stream uses Time Slicing, MPE-FEC not used";
                case 0b11: return "At least one elementary stream uses Time Slicing, At least one elementary stream uses MPE-FEC";
                default: return "unknown";
            }
        }
        private string GetBw(byte bt)
        {
            switch (bt)
            {
                case 0b00: return "7 MHz";
                case 0b01: return "8 MHz";
                case 0b10: return "6 MHz";
                default: return "other";
            }
        }
        private string GetPriority(bool pr)
        {
            return pr ? "Non-hierarchical or High Priority TS" : "Low Priority TS";
        }
    }
    public struct TxIdFunction
    {
        public ushort TxIdentifier { get; }
        public byte FunctionLoopLength { get; }
        public List<Function> Functions { get; } = null!;
        public TxIdFunction(ReadOnlySpan<byte>bytes)
        {
            var pointer = 0;
            TxIdentifier = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            FunctionLoopLength = bytes[pointer++];
            Functions = new List<Function>();

            while (pointer < FunctionLoopLength)
            {
                var fnc = GetFunction(bytes[pointer..]);
                pointer += fnc.FunctionLength + 2;
                Functions.Add(fnc);
            }
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Tx identifier: {TxIdentifier}\n";
            str += $"{prefix}Function Loop Length: {FunctionLoopLength}\n";
            foreach (var fnc in Functions)
            {
                str += fnc.Print(prefixLen + 4);
            }
            return str;
        }
        private Function GetFunction(ReadOnlySpan<byte> bytes)
        {
            try
            {
                switch (bytes[0])
                {
                    case 0x00: return new TxTimeOffsetFunction(bytes);
                    case 0x01: return new TxFrequencyOffsetFunction(bytes);
                    case 0x02: return new TxPowerFunction(bytes);
                    case 0x03: return new PrivateDataFunction(bytes);
                    case 0x04: return new CellIdFunction(bytes);
                    case 0x05: return new EnableFunction(bytes);
                    case 0x06: return new BandwidthFunction(bytes);
                    default: return new Function(bytes);
                }
            }
            catch(Exception ex)
            {
                //if something goes wrong create base function to get func length
                Logger.Send(LogStatus.Exception,$"Ecxeption while deserialise Tx Function",ex);
                return new Function(bytes);
            }            
        }
    }
    
    

    
}
