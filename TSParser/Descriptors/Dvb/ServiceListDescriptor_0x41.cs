﻿// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com 
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
using TSParser.DictionariesData;

namespace TSParser.Descriptors.Dvb
{
    internal record ServiceListDescriptor_0x41 : Descriptor
    {
        public ServiceItem[] ServiceItems { get; } = null!;
        public ServiceListDescriptor_0x41(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            ServiceItems = new ServiceItem[DescriptorLength / 3];
            for (int i = 0;i< ServiceItems.Length; i++)
            {
                ServiceItems[i] = new ServiceItem(bytes.Slice(2 + i * 3));
            }
        }
        public override string ToString()
        {
            string str = $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach(ServiceItem item in ServiceItems)
            {
                str += $"            {item}\n";
            }
            return str;
        }
    }
    public struct ServiceItem
    {
        public ushort ServiceId { get; }
        public byte ServiceType { get; }
        public string ServiceTypeName => Dictionaries.GetServiceType(ServiceType);
        public ServiceItem(ReadOnlySpan<byte> bytes)
        {
            ServiceId = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            ServiceType = bytes[2];
        }
        public override string ToString()
        {
            return $"Service id: {ServiceId}, {ServiceTypeName}";
        }
    }
}