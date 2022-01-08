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

namespace TSParser.Descriptors.AitDescriptors
{
    public record ApplicationDescriptor_0x00 : AitDescriptor
    {
        public byte ApplicationProfilesLength { get; }
        public ApplicationProfileItem[] ApplicationProfileItems { get; } = null!;
        public bool ServiceBoundFlag { get; }
        public byte Visibility { get; }
        public byte ApplicationPriority { get; }
        public TransportProtocolLabelItem[] TransportProtocolLabels { get; } = null!;
        public ApplicationDescriptor_0x00(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ApplicationProfilesLength = bytes[pointer++];
            ApplicationProfileItems = new ApplicationProfileItem[ApplicationProfilesLength / 5];
            for(var i = 0; i < ApplicationProfileItems.Length; i++)
            {
                ApplicationProfileItems[i] = new ApplicationProfileItem(bytes[pointer..]);
                pointer+=5;
            }            
            ServiceBoundFlag = (bytes[pointer] & 0x80) != 0;
            Visibility = (byte)((bytes[pointer] & 0x60) >> 5);
            //reserved 5 bits
            pointer++;
            ApplicationPriority = bytes[pointer++];            
            TransportProtocolLabels = new TransportProtocolLabelItem[DescriptorLength-pointer + 2];
            for(var i = 0;i < TransportProtocolLabels.Length; i++)
            {
                TransportProtocolLabels[i] = new TransportProtocolLabelItem(bytes[pointer++]);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}AIT Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Application Profiles Length: {ApplicationProfilesLength}\n";

            if (ApplicationProfileItems is not null)
            {
                str += $"{prefix}Application Profile Items count: {ApplicationProfileItems.Length}\n";
                foreach (var item in ApplicationProfileItems)
                {
                    str += $"{item.Print(prefixLen + 4)}";
                }
            }

            str += $"{prefix}Service Bound Flag: {ServiceBoundFlag}\n";
            str += $"{prefix}Visibility: {Visibility}\n";
            str += $"{prefix}Application Priority: {ApplicationPriority}\n";

            if (TransportProtocolLabels is not null)
            {
                str += $"{prefix}Transport Protocol Labels count: {TransportProtocolLabels.Length}\n";
                foreach (var item in TransportProtocolLabels)
                {
                    str += $"{item.Print(prefixLen + 4)}";
                }
            }
            return str;
        }
    }
    public struct ApplicationProfileItem
    {
        public ushort ApplicationProfile { get; }
        public byte VersionMajor { get; }
        public byte VersionMinor { get; }
        public byte VersionMicro { get; }
        public ApplicationProfileItem(ReadOnlySpan<byte> bytes)
        {
            ApplicationProfile = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            VersionMajor = bytes[2];
            VersionMinor = bytes[3];
            VersionMicro = bytes[4];
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Application Profile: {ApplicationProfile}, Version Major: {VersionMajor}, Version Minor: {VersionMinor}, Version Micro: {VersionMicro}\n";
        }
    }
    public struct TransportProtocolLabelItem
    {
        public byte TransportProtocolLabel { get; }
        public TransportProtocolLabelItem(byte bt)
        {
            TransportProtocolLabel = bt;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Transport Protocol Label: {TransportProtocolLabel}\n";
        }
    }
}
