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

using TSParser.DictionariesData;
using TSParser.Service;

namespace TSParser.Descriptors.Custom
{
    public record TimeZoneDescriptorLG_0xB4 : Descriptor
    {
        public byte ChannelListNameLength { get; }
        public string ChannelListName { get; }
        public ushort[] TimeZones { get; }
        public TimeZoneDescriptorLG_0xB4(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ChannelListNameLength = bytes[pointer++];
            ChannelListName = Dictionaries.BytesToString(bytes.Slice(pointer, ChannelListNameLength));
            pointer += ChannelListNameLength;
            TimeZones = new ushort[(DescriptorLength - pointer + 2)/2];
            for(int i = 0; i < TimeZones.Length; i++)
            {
                TimeZones[i] = (ushort)((bytes[pointer++] << 8) + bytes[pointer++]);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Channel list name: {ChannelListName}\n";
            foreach(var zone in TimeZones)
            {
                str += $"{prefix}Time zone: 0x{zone:X4}\n";
            }
            return str;
        }
    }
}
