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

namespace TSParser.Descriptors.Custom
{
    public struct Gnr
    {
        public ushort TransportStreamId { get; }
        public ushort OriginalNetworkId { get; }
        public ushort ServiceId { get; }
        public Gnr(ReadOnlySpan<byte> bytes)
        {
            TransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes[0..]);
            OriginalNetworkId = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            ServiceId = BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Service Id: {ServiceId}, Ts id: {TransportStreamId}, ONID: {OriginalNetworkId}\n";
        }
    }
    public record GnrDescriptor_0x86 : Descriptor
    {
        public Gnr[] Gnrs { get; }
        public GnrDescriptor_0x86(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Gnrs = new Gnr[DescriptorLength / 6];
            for (int i = 0; i < Gnrs.Length; i++)
            {
                Gnrs[i] = new Gnr(bytes.Slice(pointer, 6));
                pointer += 6;
            }
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string str = $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (Gnr gnr in Gnrs)
            {
                str += gnr.Print(prefixLen + 2);
            }
            return str;
        }
    }
}
