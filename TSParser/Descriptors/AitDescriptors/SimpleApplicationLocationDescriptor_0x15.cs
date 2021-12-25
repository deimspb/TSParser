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
using TSParser.Service;

namespace TSParser.Descriptors.AitDescriptors
{
    public record SimpleApplicationLocationDescriptor_0x15 : AitDescriptor
    {
        public byte[] InitialPathBytes { get; } = null!;
        public SimpleApplicationLocationDescriptor_0x15(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            InitialPathBytes = new byte[DescriptorLength];
            bytes.Slice(2,InitialPathBytes.Length).CopyTo(InitialPathBytes);
        }

        public override string ToString()
        {
            return $"       AIT descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, path: {Dictionaries.BytesToString(InitialPathBytes)}\n";
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}AIT descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, path: {Dictionaries.BytesToString(InitialPathBytes)}\n";
        }
    }
}
