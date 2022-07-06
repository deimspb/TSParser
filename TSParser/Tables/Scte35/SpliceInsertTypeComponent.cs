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

using TSParser.Service;

namespace TSParser.Tables.Scte35
{
    public class SpliceInsertComponent : SpliceInsertBase
    {
        public SpliceTime SpliceTime { get; }
        public byte ComponentTag { get; }
        public SpliceInsertComponent(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            ComponentTag = bytes[pointer++];
            SpliceTime = new SpliceTime(bytes[pointer..]);
            pointer += SpliceTime.SpliceTimeTypeLength;
            SpliceInsertTypeLength = pointer;
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Splice insert type component\n";

            str += $"{prefix}Component tag: {ComponentTag}\n";
            str += SpliceTime.Print(prefixLen + 4);
            return str;
        }
    }
}
