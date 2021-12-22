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
    public record LinkageDescriptor_0x4A : Descriptor
    {
        public ushort TransportStreamId { get; }
        public ushort OriginalNetworkId { get; }
        public ushort ServiceId { get; }
        public byte LinkageType { get; }
        public string LinkageTypeName=>GetLinkageType(LinkageType);
        public byte[] PrivateDataBytes { get; } = null!;
        public LinkageDescriptor_0x4A(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            TransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            OriginalNetworkId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            ServiceId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            LinkageType = bytes[pointer];
            if(LinkageType == 0x08)
            {
                Logger.Send(LogStatus.Info, $"mobile_hand-over_info not implement");//TODO: mobile_hand-over_info
            }
            else if(LinkageType == 0x0D)
            {
                Logger.Send(LogStatus.Info, $"event_linkage_info not implement");//TODO: event_linkage_info
            }
            else if(LinkageType >=0x0E && LinkageType <= 0x1F)
            {
                Logger.Send(LogStatus.Info, $"extended_event_linkage_info not implement");//TODO: extended_event_linkage_info
            }
            if (pointer < DescriptorLength)
            {
                PrivateDataBytes = new byte[DescriptorLength-pointer];
                bytes.Slice(pointer,DescriptorLength-pointer).CopyTo(PrivateDataBytes);
            }            
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, Tsid: {TransportStreamId}, Onid: {OriginalNetworkId}, Sid: {ServiceId}, type: {LinkageTypeName}";
        }
        private string GetLinkageType(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "reserved for future use";
                case 0x01: return "information service";
                case 0x02: return "EPG service";
                case 0x03: return "CA replacement service";
                case 0x04: return "TS containing complete Network/Bouquet SI";
                case 0x05: return "service replacement service";
                case 0x06: return "data broadcast service";
                case 0x07: return "RCS Map";
                case 0x08: return "mobile hand-over";
                case 0x09: return "System Software Update Service";
                case 0x0A: return "TS containing SSU BAT or NIT";
                case 0x0B: return "IP/MAC Notification Service";
                case 0x0C: return "TS containing INT BAT or NIT";
                case 0x0D: return "event linkage";
                case byte n when (n >= 0x0E && n <= 0x1F): return "extended event linkage";
                case 0x20: return "downloadable font info linkage";
                case byte n when (n >= 0x21 && n <= 0x7F): return "reserved for future use";
                case byte n when (n >= 0x80 && n <= 0xFE): return "user defined";
                case 0xFF: return "reserved for future use";
                default:
                    {
                        throw new Exception("Unknown linkage type!");
                    }
            }
        }
    }
}
