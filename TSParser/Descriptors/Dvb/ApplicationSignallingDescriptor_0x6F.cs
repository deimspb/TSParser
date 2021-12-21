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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSParser.Descriptors.Dvb
{
    public record ApplicationSignallingDescriptor_0x6F : Descriptor
    {
        public AitItem[] AitItems { get; } = null!;
        public ApplicationSignallingDescriptor_0x6F(ReadOnlySpan<byte> bytes) : base(bytes)
        {            
            AitItems = new AitItem[DescriptorLength / 3];
            for(int i = 0; i < AitItems.Length; i++)
            {
                AitItems[i] = new AitItem(bytes.Slice(2 + i * 3));
            }
        }
        public override string ToString()
        {
            string str = $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach(var item in AitItems)
            {
                str += $"            {item}";
            }
            return str;
        }
    }
    public struct AitItem
    {
        public ushort ApplicationType { get; }
        public byte AitVersion { get; }
        public AitItem(ReadOnlySpan<byte> bytes)
        {
            ApplicationType = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes) & 0x7FFF);
            AitVersion = (byte)(bytes[2] & 0x3F);
        }
        public override string ToString()
        {
            return $"Application type: {ApplicationType}, AIT version: {AitVersion}";
        }
    }

}
