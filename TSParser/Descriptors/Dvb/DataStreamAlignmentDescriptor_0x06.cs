﻿// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com 
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

namespace TSParser.Descriptors.Dvb
{
    public record DataStreamAlignmentDescriptor_0x06 : Descriptor
    {
        public byte AlignmentType { get; }
        public string AlignmentTypeName => GetAlignmentTypeName(AlignmentType);
        public DataStreamAlignmentDescriptor_0x06(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            AlignmentType = bytes[2];
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Alignment Type: {AlignmentTypeName}";
        }
        private string GetAlignmentTypeName(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "Reserved";
                case 0x01: return "Slice, or video access unit";
                case 0x02: return "Video access unit";
                case 0x03: return "GOP, or SEQ";
                case 0x04: return "SEQ";
                    default: return "Reserved";
            }
        }
    }
}