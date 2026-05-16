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
    public struct LanguageCode
    {
        public uint Code { get; }
        public byte LanguageCodeLength { get; }
        public byte ChannelListNameLength { get; }
        public string ChannelListName { get; }
        public byte ChannelListTranslationNameLength { get; }
        public string ChannelListTranslation { get; }
        public LanguageCode(ReadOnlySpan<byte> bytes)
        {
            int pointer = 0;
            Code = (uint)((bytes[pointer++] << 16) + (bytes[pointer++] << 8) + bytes[pointer++]);
            LanguageCodeLength = bytes[pointer++];
            ChannelListNameLength = bytes[pointer++];
            ChannelListName = Dictionaries.BytesToString(bytes.Slice(pointer, ChannelListNameLength));
            pointer += ChannelListNameLength;
            ChannelListTranslationNameLength = bytes[pointer++];
            ChannelListTranslation = Dictionaries.BytesToString(bytes.Slice(pointer, ChannelListTranslationNameLength));
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Channel list name: {ChannelListName}, {ChannelListTranslation}, language code: 0x{Code:X} \n";
        }

    }
    public record MultilingualRegionNameDescriptor_0x88 : Descriptor
    {
        public List<LanguageCode> LanguageCodes { get; }
        public MultilingualRegionNameDescriptor_0x88(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            int pointer = 2;
            LanguageCodes = new List<LanguageCode>();
            while (pointer < DescriptorLength)
            {
                var lc = new LanguageCode(bytes[pointer..]);
                pointer += 4 + lc.LanguageCodeLength;
                LanguageCodes.Add(lc);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";

            foreach (var languageCode in LanguageCodes)
            {
                str += languageCode.Print(prefixLen + 2);
            }
            return str;
        }
    }
}
