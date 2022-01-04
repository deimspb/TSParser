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
    public record StreamIdentifierDescriptor_0x52 : Descriptor
    {
        public byte ComponentTag { get; }
        public StreamIdentifierDescriptor_0x52(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            ComponentTag = bytes[2];
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName},tag: 0x{ComponentTag:X2}\n";
        }

    }
}
