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
    public record ExtensionDescriptor_0x7F : Descriptor
    {
        public byte DescriptorTagExtension { get; }
        public string ExtensionDescriptorName => Dictionaries.GetExtendedDescriptorName(DescriptorTagExtension);        
        public byte[] SelectorByte { get; }
        public ExtensionDescriptor_0x7F(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            DescriptorTagExtension = bytes[pointer++];
            SelectorByte = new byte[DescriptorLength - 1];
            bytes.Slice(pointer, SelectorByte.Length).CopyTo(SelectorByte);
        }

        public override string ToString()
        {
            return $"            Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {ExtensionDescriptorName}, selector bytes length: {SelectorByte.Length}";
        }
    }
}
