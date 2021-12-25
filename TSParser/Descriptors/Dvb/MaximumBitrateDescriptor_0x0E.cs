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
    public record MaximumBitrateDescriptor_0x0E : Descriptor
    {
        public uint MaximumBitrate { get; }
        public MaximumBitrateDescriptor_0x0E(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 1;
            MaximumBitrate = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]) & 0x00FFFFFF;
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Maximum bitrate: {MaximumBitrate * 50} bytes/sec";
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{prefixLen}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Maximum bitrate: {MaximumBitrate * 50} bytes/sec\n";
        }
    }
}
