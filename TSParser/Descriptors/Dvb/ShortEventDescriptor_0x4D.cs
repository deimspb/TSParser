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

namespace TSParser.Descriptors.Dvb
{
    public record ShortEventDescriptor_0x4D : Descriptor
    {
        public byte[] Iso639Code { get; }
        public string LanguageCode { get; }
        public byte EventNameLength { get; }
        public string EventName { get; }
        public byte TextLength { get; }
        public string Text { get; }
        public ShortEventDescriptor_0x4D(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Iso639Code = new byte[3];
            pointer += 3;
            bytes.Slice(2, 3).CopyTo(Iso639Code);
            LanguageCode = Dictionaries.BytesToString(Iso639Code);
            EventNameLength = bytes[pointer++];
            EventName = Dictionaries.BytesToString(bytes.Slice(pointer, EventNameLength));
            pointer += EventNameLength;
            TextLength = bytes[pointer++];
            Text = Dictionaries.BytesToString(bytes.Slice(pointer, TextLength));

        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {EventName}, {Text}\n";
        }
    }
}
