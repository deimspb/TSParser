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
    public record CaDescriptor_0x09 : Descriptor
    {
        public ushort CaSystemId { get; }
        public ushort CaPid { get; }
        public byte[] PrivateDateBytes { get; }
        public CaDescriptor_0x09(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            CaSystemId = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            CaPid = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) & 0x1FFF);
            PrivateDateBytes = new byte[DescriptorLength - 4];
            bytes.Slice(6, PrivateDateBytes.Length).CopyTo(PrivateDateBytes);
        }

        public override string ToString()
        {
            return $"         CA system id: {CaSystemId}, CA pid: {CaPid}, private data: {BitConverter.ToString(PrivateDateBytes):X}";           
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);

            return $"{header}CA system id: {CaSystemId}, CA pid: {CaPid}, private data: {BitConverter.ToString(PrivateDateBytes):X}\n";
        }
    }
}
