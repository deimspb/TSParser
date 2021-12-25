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

using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record StdDescriptor_0x11 : Descriptor
    {
        public bool LeakValidFlag { get; }
        public StdDescriptor_0x11(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            LeakValidFlag = (bytes[2] & 0x01) != 0;
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Leak Valid Flag: {LeakValidFlag}\n";
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Leak Valid Flag: {LeakValidFlag}\n";
        }
    }
}
