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

namespace TSParser.Descriptors.AitDescriptors
{
    public record DvbJApplicationLocationDescriptor_0x04 : AitDescriptor
    {
        public byte BaseDirectoryLength { get; }
        public string BaseDirectory { get; }
        public byte ClasspathExtensionLength { get; }
        public string ClasspathExtension { get; }
        public string InitialClass { get; }

        public DvbJApplicationLocationDescriptor_0x04(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            BaseDirectoryLength = bytes[pointer++];
            BaseDirectory = Dictionaries.BytesToString(bytes.Slice(pointer, BaseDirectoryLength));
            pointer += BaseDirectoryLength;
            ClasspathExtensionLength = bytes[pointer++];
            ClasspathExtension = Dictionaries.BytesToString(bytes.Slice(pointer, ClasspathExtensionLength));
            pointer += ClasspathExtensionLength;
            InitialClass = Dictionaries.BytesToString(bytes.Slice(pointer, DescriptorLength - pointer + 2));
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}AIT Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Base Directory: {BaseDirectory}\n";
            str += $"{prefix}Class path Extension: {ClasspathExtension}\n";
            str += $"{prefix}Initial Class: {InitialClass}\n";
            return str;
        }
    }
}
