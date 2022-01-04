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

namespace TSParser.Descriptors.Scte35Descriptors
{
    public record DtmfDescriptor_0x01 : Scte35Descriptor
    {
        public string Identifier { get; }
        public byte Preroll { get; }
        public byte DtmfCount { get; }
        public string DtmfChar { get; }
        public DtmfDescriptor_0x01(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Identifier = Dictionaries.BytesToString(bytes.Slice(pointer, 4));
            pointer += 4;
            Preroll = bytes[pointer++];
            DtmfCount = (byte)((bytes[pointer++] & 0xE0) >> 5);
            //reserved 5 bits
            DtmfChar = Dictionaries.BytesToString(bytes.Slice(pointer, DtmfCount));
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Splice descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, {DescriptorLength}\n";
            str += $"{prefix}Identifier: {Identifier}\n";
            str += $"{prefix}Preroll: {Preroll}\n";
            str += $"{prefix}Dtmf Count: {DtmfCount}\n";
            str += $"{prefix}Dtmf Char: {DtmfChar}\n";
            return str;
        }
    }
}
