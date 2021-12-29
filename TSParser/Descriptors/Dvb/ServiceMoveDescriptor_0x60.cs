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
    public record ServiceMoveDescriptor_0x60 : Descriptor
    {
        public ushort NewOriginalNetworkId { get; }
        public ushort NewTransportStreamId { get; }
        public ushort NewServiceId { get; }
        public ServiceMoveDescriptor_0x60(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            NewOriginalNetworkId = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            NewTransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
            NewServiceId = BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]);
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Move service to Onid: {NewOriginalNetworkId}, transport stream: {NewTransportStreamId}, new service id: {NewServiceId}\n";
            return str;
        }
    }
}
