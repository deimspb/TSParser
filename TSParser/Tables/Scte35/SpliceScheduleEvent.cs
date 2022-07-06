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

namespace TSParser.Tables.Scte35
{
    public class SpliceScheduleEvent
    {
        public int EventLength { get; }
        public bool ProgramSpliceFlag { get; }
        public bool DurationFlag { get; }

        public EventBase[] SpliceScheduleTypeEvents { get; }
        public BreakDuration BreakDuration { get; }
        public uint SpliceEventId { get; }
        public bool SpliceEventCancelIndicator { get; }
        public bool OutOfNetworkIndicator { get; }
        public ushort UniqueProgramId { get; }
        public byte AvailNum { get; }
        public byte AvailsExpected { get; }
        public SpliceScheduleEvent(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            SpliceEventId = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            SpliceEventCancelIndicator = (bytes[pointer++] & 0x80) != 0;
            if (!SpliceEventCancelIndicator)
            {
                OutOfNetworkIndicator = (bytes[pointer] & 0x80) != 0;
                ProgramSpliceFlag = (bytes[pointer] & 0x40) != 0;
                DurationFlag = (bytes[pointer++] & 0x20) != 0;
                if (ProgramSpliceFlag)
                {
                    SpliceScheduleTypeEvents = new EventBase[] { new EventProgram(bytes.Slice(pointer, 4)) };
                    pointer += 4;
                }
                else
                {
                    var componentCount = bytes[pointer++];
                    SpliceScheduleTypeEvents = new EventComponent[componentCount];

                    for (int i = 0; i < componentCount; i++)
                    {
                        SpliceScheduleTypeEvents[i] = new EventComponent(bytes.Slice(pointer, 5));
                        pointer += 5;
                    }
                }
                if (DurationFlag)
                {
                    BreakDuration = new BreakDuration(bytes.Slice(pointer, 5));
                }
                UniqueProgramId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
                pointer += 2;
                AvailNum = bytes[pointer++];
                AvailsExpected = bytes[pointer++];
            }
            EventLength = pointer;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Splice Schedule Event\n";
            str += $"{prefix}Splice Event id: {SpliceEventId}\n";
            str += $"{prefix}Splice Event cancel indicator: {SpliceEventCancelIndicator}\n";
            str += $"{prefix}Out of network indicator: {OutOfNetworkIndicator}\n";
            str += $"{prefix}Program splice flag: {ProgramSpliceFlag}\n";
            str += $"{prefix}Duration flag: {DurationFlag}\n";
            
            foreach(var item in SpliceScheduleTypeEvents)
            {
                str += item.Print(prefixLen + 4);
            }

            str += BreakDuration.Print(prefixLen + 4);
            str += $"{prefix}Unique program id: {UniqueProgramId}\n";
            str += $"{prefix}Avail num: {AvailNum}\n";
            str += $"{prefix}Avail expected: {AvailsExpected}\n";

            return str;
        }
    }
}
