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
    public struct StbId
    {
        public byte Id { get; }
        public string StbIdName => CustomDictionaries.GetStbName(Id);
        public StbId(byte bt)
        {
            Id = bt;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Stb: {StbIdName}, Value: {Id}\n";
        }
    }
    public struct ChannelListType
    {
        public byte ChannelListTypePostfixLength { get; }
        public string ChannelListPostfix { get; }
        public byte StbIdLength { get; }
        public StbId[] StbIds { get; }
        public ChannelListType(ReadOnlySpan<byte> bytes)
        {
            int pointer = 0;
            ChannelListTypePostfixLength = bytes[pointer++];
            ChannelListPostfix = Dictionaries.BytesToString(bytes.Slice(pointer, ChannelListTypePostfixLength));
            pointer += ChannelListTypePostfixLength;
            StbIdLength = bytes[pointer++];
            StbIds = new StbId[StbIdLength];
            for (int i = 0; i < StbIdLength; i++)
            {
                StbIds[i] = new StbId(bytes[pointer++]);
            }
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string str = $"{header}Channel list type: {ChannelListPostfix}\n";
            foreach (StbId id in StbIds)
            {
                str += id.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public record ChannelListTypeDescriptor_0xB2 : Descriptor
    {
        public List<ChannelListType> ChannelListTypes { get; }
        public ChannelListTypeDescriptor_0xB2(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            int pointer = 2;
            ChannelListTypes = new List<ChannelListType>();

            while (pointer < DescriptorLength)
            {
                var clt = new ChannelListType(bytes[pointer..]);
                pointer += clt.ChannelListTypePostfixLength + clt.StbIdLength + 2;
                ChannelListTypes.Add(clt);
            }

        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var channel in ChannelListTypes)
            {
                str += channel.Print(prefixLen + 2);
            }
            return str;
        }

    }
}
