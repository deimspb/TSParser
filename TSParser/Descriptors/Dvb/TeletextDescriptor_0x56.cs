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
    public record TeletextDescriptor_0x56 : Descriptor
    {
        public Language[] Languages { get; }
        public TeletextDescriptor_0x56(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            Languages = new Language[DescriptorLength / 5];

            for (int i = 0; i < Languages.Length; i++)
            {
                Languages[i] = new Language(bytes[(2 + i * 5)..]);
            }

        }

        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var ln in Languages)
            {
                str += ln.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct Language
    {
        public byte[] LanguageCode { get; }
        public string LanguageName => Dictionaries.BytesToString(LanguageCode);
        public byte TeletextType { get; }
        public string TeletextTypeName => Dictionaries.GetTeletextTypeStr(TeletextType);
        public byte TeletextMagazinNumber { get; }
        public byte TeletextPageNumber { get; }
        public Language(ReadOnlySpan<byte> bytes)
        {
            LanguageCode = new byte[3];
            bytes[0..3].CopyTo(LanguageCode);
            TeletextType = (byte)(bytes[3] >> 3);
            TeletextMagazinNumber = (byte)(bytes[3] & 0x7);
            TeletextPageNumber = bytes[4];
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Language: {LanguageName}, Teletext type: {TeletextTypeName}, Page number: {TeletextPageNumber}\n";
        }
    }
}
