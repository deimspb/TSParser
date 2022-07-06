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
    public class EventComponent:EventBase
    {
        public byte ComponentTag { get; }
        public uint UtcSpliceTime { get; }
        public EventComponent(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            ComponentTag = bytes[pointer++];
            UtcSpliceTime = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Event component\r";
            str += $"{prefix}Component tag: {ComponentTag}\r";
            str += $"{prefix}Utc splice time: {Utils.GetPtsDtsValue(UtcSpliceTime)}\r";

            return str;
        }
    }
}
