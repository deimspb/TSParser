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

using System.Buffers.Binary;
using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record MultiplexBufferUtilizationDescriptor_0x0C : Descriptor
    {
        public bool BoundValidFlag { get; }
        public ushort LtwOffsetLowerBound { get; }
        public ushort LtwOffsetUpperBound { get; }
        public MultiplexBufferUtilizationDescriptor_0x0C(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            BoundValidFlag = (bytes[pointer] & 0x80) != 0;
            LtwOffsetLowerBound = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x7FFF);
            //reserved 1 bit
            pointer += 2;
            LtwOffsetUpperBound = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x7FFF);
        }
        
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Bound Valid Flag: {BoundValidFlag}, LtwOffsetLowerBound: {LtwOffsetLowerBound}, LtwOffsetUpperBound: {LtwOffsetUpperBound}\n";
        }
    }
}
