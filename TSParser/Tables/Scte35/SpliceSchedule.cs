﻿// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com 
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

namespace TSParser.Tables.Scte35
{
    public record SpliceSchedule : SpliceCommand
    {
        public byte SpliceCount { get; }
        public SpliceScheduleEvent[] Events { get; }

        public SpliceSchedule(ReadOnlySpan<byte> bytes, byte spliceType) : base(bytes, spliceType)
        {
            var pointer = 0;
            SpliceCount = bytes[pointer++];
            Events = new SpliceScheduleEvent[SpliceCount];
            for (int i = 0; i < SpliceCount; i++)
            {
                Events[i] = new SpliceScheduleEvent(bytes[pointer..]);
                pointer += Events[i].EventLength;
            }
        }

        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Splice schedule, type: {SpliceCommandType}\r";

            foreach(var item in Events)
            {
                str += item.Print(prefixLen + 4);
            }
            return str;
        }
    }
}
