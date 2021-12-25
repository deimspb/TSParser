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

namespace TSParser.Descriptors.Dvb
{
    public record CueIdentifierDescriptor_0x8A : Descriptor
    {
        public byte CueStreamType { get; }
        public string CueStreamTypeName =>GetCueStreamTypeName(CueStreamType);
        public CueIdentifierDescriptor_0x8A(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            CueStreamType = bytes[2];
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName} Cue stream type: {CueStreamTypeName}\n";
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName} Cue stream type: {CueStreamTypeName}\n";
        }
        private string GetCueStreamTypeName(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "splice_insert, splice_null, splice_schedule";
                case 0x01: return "All Commands";
                case 0x02: return "Segmentation";
                case 0x03: return "Tiered Splicing";
                case 0x04: return "Tiered Segmentation";
                case byte n when n >= 0x05 && n <= 0x7F: return "Reserved";
                    default: return "User Defined";

            }
        }
    }
}
