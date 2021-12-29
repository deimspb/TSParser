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

namespace TSParser.Tables.Scte35
{
    public record SpliceInsert : SpliceCommand
    {
        public uint SpliceEventId { get; }
        public bool SpliceEventCancelIndicator { get; }
        public bool OutOfNetworkIndicator { get; }
        public bool ProgramSpliceFlag { get; }
        public bool DurationFlag { get; }
        public bool SpliceImmediateFlag { get; }
        public SpliceTime spliceTime { get; }
        public byte ComponentCount { get; }
        public SpliceComponent[] SpliceComponents { get; } = null!;
        public BreakDuration breakDuration { get; }
        public ushort UniqueProgramId { get; }
        public byte AvailNum { get; }
        public byte AvailsExpected { get; }

        public SpliceInsert(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 0;
            SpliceEventId = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            SpliceEventCancelIndicator = (bytes[pointer++] & 0x80) != 0;
            //reserved 7 bits
            if (!SpliceEventCancelIndicator)
            {
                OutOfNetworkIndicator = (bytes[pointer] & 0x80) != 0;
                ProgramSpliceFlag = (bytes[pointer] & 0x40) != 0;
                DurationFlag = (bytes[pointer] & 0x20) != 0;
                SpliceImmediateFlag = (bytes[pointer++]&0x10)!= 0;
                //reserved 4 bits
                if(ProgramSpliceFlag && !SpliceImmediateFlag)
                {
                    spliceTime = new SpliceTime(bytes.Slice(pointer,5));
                }
                if (!ProgramSpliceFlag)
                {
                    ComponentCount = bytes[pointer++];
                    SpliceComponents = new SpliceComponent[ComponentCount];
                    for(int i = 0; i < ComponentCount; i++)
                    {
                        SpliceComponents[i] = new SpliceComponent(bytes[pointer..],SpliceImmediateFlag);
                        pointer += SpliceImmediateFlag ? 1 : 6;
                    }
                }
                if (DurationFlag)
                {
                    breakDuration = new(bytes.Slice(pointer, 5));
                }
                UniqueProgramId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
                pointer += 2;
                AvailNum = bytes[pointer++];
                AvailsExpected = bytes[pointer++];
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Splice Insert Command\n";
            str += $"{prefix}Splice event id: {SpliceEventId}\n";
            str += $"{prefix}Splice Event Cancel Indicator: {SpliceEventCancelIndicator}\n";

            if (!SpliceEventCancelIndicator)
            {
                str += $"{prefix}Out Of Network Indicator: {OutOfNetworkIndicator}\n";
                str += $"{prefix}Program Splice Flag: {ProgramSpliceFlag}\n";
                str += $"{prefix}Duration Flag: {DurationFlag}\n";
                str += $"{prefix}Splice Immediate Flag: {SpliceImmediateFlag}\n";
                if(ProgramSpliceFlag && !SpliceImmediateFlag)
                {
                    str += spliceTime.Print(prefixLen + 4);
                }
                if (!ProgramSpliceFlag)
                {
                    str += $"{prefix}Component count: {ComponentCount}\n";
                    if(ComponentCount > 0)
                    {
                        foreach(var component in SpliceComponents)
                        {
                            str+=component.Print(prefixLen + 4);
                        }
                    }
                }
                if (DurationFlag)
                {
                    str += breakDuration.Print(prefixLen + 4);
                }

                str += $"{prefix}Unique Program Id: {UniqueProgramId}\n";
                str += $"{prefix}Avail Num: {AvailNum}\n";
                str += $"{prefix}Avails Expected: {AvailsExpected}\n";
            }

            return str;
        }
    }
    public struct SpliceComponent
    {
        public byte ComponentTag { get; }
        public SpliceTime spliceTime { get; }

        public SpliceComponent(ReadOnlySpan<byte> bytes,bool SpliceImmediateFlag)
        {
            ComponentTag = bytes[0];
            spliceTime = SpliceImmediateFlag ? new SpliceTime(bytes.Slice(1, 5)) : default;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Component tag: {ComponentTag}, Splice time: {spliceTime.Print(prefixLen + 2)}\n";
        }
    }
    public struct SpliceTime
    {
        public bool TimeSpecificFlag { get; }
        public ulong PtsTime { get; }
        public SpliceTime(ReadOnlySpan<byte> bytes)
        {
            //reserved 6 bits
            TimeSpecificFlag = (bytes[0] & 0x80) != 0;
            if (TimeSpecificFlag)
            {
                PtsTime = (ulong)((bytes[0] & 0x01) << 32 | bytes[1] << 24 | bytes[2] << 16 | bytes[3] << 8 | bytes[4]);
            }
            else
            {
                PtsTime = default;
            }
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Splice time, time specific flag: {TimeSpecificFlag}, pts time: 0x{PtsTime:X}\n";
        }
    }
    public struct BreakDuration
    {
        public bool AutoReturn { get; }
        public ulong Duration { get; }
        public BreakDuration(ReadOnlySpan<byte> bytes)
        {
            AutoReturn = (bytes[0] & 0x80) != 0;
            Duration = (ulong)((bytes[0] & 0x01) << 32 | bytes[1] << 24 | bytes[2] << 16 | bytes[3] << 8 | bytes[4]);
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Break duration, auto return: {AutoReturn}, duration: 0x{Duration:X}\n";
        }
    }
}
