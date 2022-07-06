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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSParser.Service;

namespace TSParser.Descriptors.Scte35Descriptors
{
    public record AudioDescriptor_0x04 : Scte35Descriptor
    {
        public byte AudioCount { get; }
        public AudioChanType[] AudioChannels { get; }
        public AudioDescriptor_0x04(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 6;
            AudioCount = (byte)(bytes[pointer++] >> 4);
            //reserved 4 bits
            AudioChannels = new AudioChanType[AudioCount];

            for (int i = 0; i < AudioCount; i++)
            {
                AudioChannels[i] = new AudioChanType(bytes[pointer..]);
                pointer += 5;
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Splice audio descriptor\n";

            str += $"{prefix}Audio Count: {AudioCount}\n";

            foreach (var item in AudioChannels)
            {
                str += item.Print(prefixLen + 4);
            }

            return str;
        }
    }
    public struct AudioChanType
    {
        public string ISOCode { get; }
        public byte BitStreamMode { get; }
        public byte NumChannels { get; }
        public byte FullSrvcAudio { get; }
        public byte ComponentTag { get; }
        public AudioChanType(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            ComponentTag = bytes[pointer++];
            ISOCode = Encoding.UTF8.GetString(bytes.Slice(pointer, 3));
            pointer += 3;
            BitStreamMode = (byte)((bytes[pointer] & 0xE0) >> 5);
            NumChannels = (byte)((bytes[pointer] & 0x1E) >> 1);
            FullSrvcAudio = (byte)(bytes[pointer] & 0x01);
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Audio channel type\n";
            str += $"{prefix}Component Tag: {ComponentTag}\n";
            str += $"{prefix}ISO Code: {ISOCode}\n";
            str += $"{prefix}Bit Stream Mode: {BitStreamMode}\n";
            str += $"{prefix}Num Channels: {NumChannels}\n";
            str += $"{prefix}Full Srvc Audio: {FullSrvcAudio}\n";

            return str;
        }
    }
}
