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

using System.Buffers.Binary;
using TSParser.DictionariesData;
using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record DataBroadcastDescriptor_0x64 : Descriptor
    {
        public ushort DataBroadcastId { get; }
        public byte ComponentTag { get; }
        public byte SelectorLength { get; }
        public byte[] SelectorByte { get; } = null!;//TODO: impement with ETSI TS 101 162
        public string Iso639LanguageCode { get; }
        public byte TextLength { get; }
        public string TextChar { get; }
        public DataBroadcastDescriptor_0x64(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            DataBroadcastId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            ComponentTag = bytes[pointer++];
            SelectorLength = bytes[pointer++];
            SelectorByte = new byte[SelectorLength];
            bytes.Slice(pointer, SelectorLength).CopyTo(SelectorByte);
            pointer += SelectorLength;
            Iso639LanguageCode = Dictionaries.BytesToString(bytes.Slice(pointer, 3));
            pointer += 3;
            TextLength = bytes[pointer++];
            TextChar = Dictionaries.BytesToString(bytes.Slice(pointer,TextLength));
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Data Broadcast Id: 0x{DataBroadcastId}\n";
            str += $"{prefix}Component Tag: {ComponentTag}\n";
            str += $"{prefix}Selector Length: {SelectorLength}\n";
            str += $"{prefix}Selector Byte: {BitConverter.ToString(SelectorByte):X}\n";
            str += $"{prefix}Iso639 Language Code: {Iso639LanguageCode}\n";
            str += $"{prefix}Text Length: {TextLength}\n";
            str += $"{prefix}Text Char: {TextChar}\n";
            return str;
        }
    }
}
