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

namespace TSParser.Descriptors.Dvb
{
    public record MultilingualBouquetNameDescriptor_0x5C : Descriptor
    {
        public uint ISO_639_LanguageCode { get; }
        public byte BouquetNameLength { get; }
        public string BouquetName { get; }
        public MultilingualBouquetNameDescriptor_0x5C(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ISO_639_LanguageCode = (uint)((bytes[pointer++] << 16) + (bytes[pointer++] << 8) + bytes[pointer++]);
            BouquetNameLength = bytes[pointer++];
            BouquetName = Dictionaries.BytesToString(bytes.Slice(pointer, BouquetNameLength));
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {BouquetName}";
        }
    }
}
