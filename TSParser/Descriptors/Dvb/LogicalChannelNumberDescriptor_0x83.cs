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
    public record LogicalChannelNumberDescriptor_0x83 : Descriptor
    {
        public LcnItem[] LcnItems { get; }
        public LogicalChannelNumberDescriptor_0x83(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            LcnItems = new LcnItem[DescriptorLength / 4];
            for(int i = 0; i < LcnItems.Length; i++)
            {
                LcnItems[i] = new LcnItem(bytes.Slice(2 + i * 4));
            }
        }        
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var item in LcnItems)
            {
                str += item.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct LcnItem
    {
        public ushort ServiceID { get; }
        public bool VisisbleServiceDlag { get; }
        public ushort LogicalChannelNumber { get; }
        public LcnItem(ReadOnlySpan<byte> bytes)
        {
            ServiceID = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            VisisbleServiceDlag = (bytes[2] & 0x80) != 0;
            LogicalChannelNumber = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]) & 0x03ff);
        }        
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Service id:{ServiceID}, visible:{VisisbleServiceDlag}, lcn: {LogicalChannelNumber}\n";
        }
    }
}
