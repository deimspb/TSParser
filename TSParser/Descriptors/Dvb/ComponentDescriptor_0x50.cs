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
    public record ComponentDescriptor_0x50 : Descriptor
    {
        public byte StreamContentExt { get; }//TODO: impement better with table 26 ETSI EN 300 468 V1.16.1
        public byte StreamContent { get; }
        public byte ComponentType { get; }
        public byte ComponentTag { get; }
        public byte[] Iso639LanguageCode { get; }
        public string TextChar { get; }
        public ComponentDescriptor_0x50(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            StreamContentExt = (byte)((bytes[pointer] & 0xF0)>>4);
            StreamContent = (byte)(bytes[pointer++] & 0x0F);
            ComponentType = bytes[pointer++];
            ComponentTag = bytes[pointer++];
            Iso639LanguageCode = new byte[] { bytes[pointer++], bytes[pointer++], bytes[pointer++] };
            TextChar = Dictionaries.BytesToString(bytes.Slice(pointer,DescriptorLength-6));
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Stream Content Ext: 0x{StreamContentExt:X}\n";
            str += $"{prefix}Stream Content: 0x{StreamContent}\n";
            str += $"{prefix}Component Type: 0x{ComponentType}\n";
            str += $"{prefix}Component Tag: 0x{ComponentTag}\n";
            str += $"{prefix}Iso639 Language Code: {Dictionaries.BytesToString(Iso639LanguageCode)}\n";
            str += $"{prefix}{TextChar}\n";
            return str;
        }
    }
}
