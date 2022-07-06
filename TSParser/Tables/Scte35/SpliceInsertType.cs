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
        public bool ProgramSpliceFlag { get; }
        public bool DurationFlag { get; }


        public SpliceInsertBase[] SpliceInserts { get; }
        public BreakDuration BreakDuration { get; }
        public uint SpliceEventId { get; }
        public bool SpliceEventCancelIndicator { get; }
        public bool OutOfNetworkIndicator { get; }
        public bool SpliceImmediateFlag { get; }
        public ushort UniqueProgramId { get; }
        public byte AvailNum { get; }
        public byte AvailsExpected { get; }
        public SpliceInsert(ReadOnlySpan<byte> bytes, byte spliceType) : base(bytes, spliceType)
        {
            var pointer = 0;

            SpliceEventId = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            SpliceEventCancelIndicator = (bytes[pointer++] & 0x80) != 0;
            // reserved 7 bits
            if (!SpliceEventCancelIndicator)
            {
                OutOfNetworkIndicator = (bytes[pointer] & 0x80) != 0;
                ProgramSpliceFlag = (bytes[pointer] & 0x40) != 0;
                DurationFlag = (bytes[pointer] & 0x20) != 0;
                SpliceImmediateFlag = (bytes[pointer++] & 0x10) != 0;
                //reserved 4 bits
                if (ProgramSpliceFlag && !SpliceImmediateFlag)
                {
                    SpliceInserts = new SpliceInsertProgram[] { new SpliceInsertProgram(bytes[pointer..]) };
                    pointer += SpliceInserts[0].SpliceInsertTypeLength;
                }
                if (!ProgramSpliceFlag)
                {
                    var componentCount = bytes[pointer++];
                    SpliceInserts = new SpliceInsertComponent[componentCount];
                    for (var i = 0; i < componentCount; i++)
                    {
                        var component = new SpliceInsertComponent(bytes[pointer..]);
                        pointer += component.SpliceInsertTypeLength;
                        SpliceInserts[i] = component;
                    }
                }
                if (DurationFlag)
                {
                    BreakDuration = new BreakDuration(bytes[pointer..]);
                    pointer += 5;
                }

            }
            UniqueProgramId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            AvailNum = bytes[pointer++];
            AvailsExpected = bytes[pointer++];

            SpliceCommandLength = pointer;
        }

        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Splice Insert Command\n";
            str += $"{prefix}Splice event id: {SpliceEventId}\n";
            str += $"{prefix}Splice event cancel indicator: {SpliceEventCancelIndicator}\n";

            if (!SpliceEventCancelIndicator)
            {
                str += $"{prefix}Out of network indicator: {OutOfNetworkIndicator}\n";
                str += $"{prefix}Program splice flag: {ProgramSpliceFlag}\n";
                str += $"{prefix}Duration flag: {DurationFlag}\n";
                str += $"{prefix}Splice immediate flag: {SpliceImmediateFlag}\n";

                foreach (var item in SpliceInserts)
                {
                    str += item.Print(prefixLen + 4);
                }

                if (DurationFlag)
                {
                    str += BreakDuration.Print(prefixLen + 4);
                }
            }
            str += $"{prefix}Unique program id: {UniqueProgramId}\n";
            str += $"{prefix}Avail num: {AvailNum}\n";
            str += $"{prefix}Avail expected: {AvailsExpected}\n";

            return str;
        }
    }

    public struct SpliceTime
    {
        public int SpliceTimeTypeLength { get; }
        public bool TimeSpecificFlag { get; }
        public ulong PtsTime { get; }
        public SpliceTime(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            //reserved 6 bits
            TimeSpecificFlag = (bytes[pointer] & 0x80) != 0;
            if (TimeSpecificFlag)
            {
                PtsTime = (ulong)(bytes[pointer++] & 0x01) << 32
                    | (ulong)(bytes[pointer++]) << 24
                    | (ulong)(bytes[pointer++]) << 16
                    | (ulong)(bytes[pointer++]) << 8
                    | bytes[pointer++];
            }
            else
            {
                PtsTime = default;
            }
            SpliceTimeTypeLength = pointer;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Splice time, time specific flag: {TimeSpecificFlag}, pts time: {Utils.GetPtsDtsValue(PtsTime)}\n";
        }
    }
    public struct BreakDuration
    {
        public bool AutoReturn { get; }
        public ulong Duration { get; }
        public BreakDuration(ReadOnlySpan<byte> bytes)
        {
            AutoReturn = (bytes[0] & 0x80) != 0;
            Duration = (ulong)(bytes[0] & 0x01) << 32 | (ulong)(bytes[1]) << 24 | (ulong)(bytes[2]) << 16 | (ulong)(bytes[3]) << 8 | bytes[4];
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Break duration, auto return: {AutoReturn}, duration: {Utils.GetPtsDtsValue(Duration)}\n";
        }
    }
}
