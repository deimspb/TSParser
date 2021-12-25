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
    public record Iso639LanguageDescriptor_0x0A : Descriptor
    {
        public Iso639Record[] iso639Records { get; }
        public Iso639LanguageDescriptor_0x0A(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            iso639Records = new Iso639Record[DescriptorLength / 4];

            for (int i = 0; i < iso639Records.Length; i++)
            {
                iso639Records[i] = new Iso639Record(bytes[(2 + i * 4)..]);
            }
        }

        public override string ToString()
        {
            string str = $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var ir in iso639Records)
            {
                str += $"{ir}";
            }
            return str;
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var ir in iso639Records)
            {
                str += ir.Print(prefixLen+2);
            }
            return str;
        }
    }

    public readonly struct Iso639Record
    {
        public byte[] Iso639LanguageCode { get; }
        public string LanguageCode => Dictionaries.BytesToString(Iso639LanguageCode);
        public byte AudioType { get; }
        public string AudioTypeName => Dictionaries.AudioTypes.TryGetValue(AudioType, out string? str) ? str : "Unknown";
        public Iso639Record(ReadOnlySpan<byte> bytes)
        {
            Iso639LanguageCode = new byte[3];
            bytes[0..3].CopyTo(Iso639LanguageCode);
            AudioType = bytes[3];
        }

        public override string ToString()
        {
            return $"            Language code: {LanguageCode}, Audio type: {AudioTypeName}";
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Language code: {LanguageCode}, Audio type: {AudioTypeName}\n";
        }
    }

}
