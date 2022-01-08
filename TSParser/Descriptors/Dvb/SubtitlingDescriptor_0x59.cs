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
    public record SubtitlingDescriptor_0x59 : Descriptor
    {
        public Subtitle[] Subtitles { get; } = null!;
        public SubtitlingDescriptor_0x59(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            Subtitles = new Subtitle[DescriptorLength / 8];
            for (int i = 0; i < Subtitles.Length; i++)
            {
                Subtitles[i] = new Subtitle(bytes[(2 + i * 8)..]);
            }
        }        
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (Subtitle subtitle in Subtitles)
            {
                str += subtitle.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct Subtitle
    {
        public string Iso639LanguageCode { get; }
        public byte SubtitlingType { get; } //TODO: impement table 26 from ETSI EN 300 468 v1.16.1
        public ushort CompositionPageId { get; }
        public ushort AncillaryPageId { get; }
        public Subtitle(ReadOnlySpan<byte> bytes)
        {
            Iso639LanguageCode = Dictionaries.BytesToString(bytes[..3]);
            SubtitlingType = bytes[3];
            CompositionPageId = BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
            AncillaryPageId = BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]);
        }        
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Subtitle language: {Iso639LanguageCode}, Type: {SubtitlingType}, Composition Page Id: {CompositionPageId}, Ancillary Page Id: {AncillaryPageId}\n";
        }
    }
}
