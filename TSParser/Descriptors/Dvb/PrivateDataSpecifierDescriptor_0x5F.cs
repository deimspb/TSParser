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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record PrivateDataSpecifierDescriptor_0x5F : Descriptor
    {
        public uint PrivateDataSpecifier { get; }
        public PrivateDataSpecifierDescriptor_0x5F(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            PrivateDataSpecifier = BinaryPrimitives.ReadUInt16BigEndian(bytes);
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Private data specifier: 0x{PrivateDataSpecifier:X}\n";
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Private data specifier: 0x{PrivateDataSpecifier:X}\n";
        }
    }
}
