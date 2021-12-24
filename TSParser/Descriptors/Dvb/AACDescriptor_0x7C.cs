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
using TSParser.DictionariesData;

namespace TSParser.Descriptors.Dvb
{
    public record AACDescriptor_0x7C : Descriptor
    {
        public byte ProfileAndLevel { get; }
        public string ProfileName =>Dictionaries.GetMpeg4AudioProfileAndLevelValue(ProfileAndLevel);
        public bool AacTypeFlag { get; }
        public bool SaocDeFlag { get; }
        public byte AacType { get; }
        public byte[] AdditionalInfoByte { get; } = null!;
        public AACDescriptor_0x7C(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ProfileAndLevel = bytes[pointer++];
            if (DescriptorLength > 1)
            {
                AacTypeFlag = (bytes[pointer] & 0x80) != 0;
                SaocDeFlag = (bytes[pointer++] & 0x40) != 0;
                //reserved 6 bits
                if (AacTypeFlag)
                {
                    AacType = bytes[pointer++];
                }
                if (DescriptorLength - pointer > 0)
                {
                    AdditionalInfoByte = new byte[DescriptorLength-pointer];
                    bytes.Slice(pointer,AdditionalInfoByte.Length).CopyTo(AdditionalInfoByte);
                }
            }
        }
        public override string ToString()
        {
            string str = $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"            Profile And Level: {ProfileName}\n";
            str += $"            Aac Type Flag: {AacTypeFlag}\n";
            return str;
        }
    }
}
