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
using TSParser.DictionariesData;
using TSParser.Service;

namespace TSParser.Descriptors.Custom
{
    public struct ChannelList
    {
        public byte ChannelListId { get; }
        public byte ChannelListNameLength { get; }
        public string ChannelListName { get; }
        public uint CountryCode { get; }
        public byte ServiceIdListLength { get; }
        public LcnService[] LcnServices { get; }
        public ChannelList(ReadOnlySpan<byte> bytes)
        {
            int pointer = 0;
            ChannelListId = bytes[pointer++];
            ChannelListNameLength = bytes[pointer++];
            ChannelListName = Dictionaries.BytesToString(bytes.Slice(pointer, ChannelListNameLength));
            pointer += ChannelListNameLength;
            CountryCode = (uint)((bytes[pointer++] << 16) + (bytes[pointer++] << 8) + bytes[pointer++]);
            ServiceIdListLength = bytes[pointer++];
            LcnServices = new LcnService[ServiceIdListLength / 4];
            for (int i = 0; i < LcnServices.Length; i++)
            {
                LcnServices[i] = new LcnService(bytes.Slice(pointer, 4));
                pointer += 4;
            }
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string str = $"{header}Channel list name: {ChannelListName}, Channel list id: {ChannelListId}\n";
            foreach (LcnService service in LcnServices)
            {
                str += service.Print(prefixLen + 2);
            }
            return str;
        }

    }
    public struct LcnService
    {
        public ushort ServiceId { get; }
        public bool VisibleServiceFlag { get; }
        public bool VpfStb { get; }
        public bool VpfCam { get; }
        public bool VpfTv { get; }
        public bool Vpf4 { get; }
        public bool Vpf5 { get; }
        public ushort Lcn { get; }
        public LcnService(ReadOnlySpan<byte> bytes)
        {
            int pointer = 0;
            ServiceId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            VisibleServiceFlag = (bytes[pointer] & 0x80) != 0;
            VpfStb = (bytes[pointer] & 0x40) != 0;
            VpfCam = (bytes[pointer] & 0x20) != 0;
            VpfTv = (bytes[pointer] & 0x10) != 0;
            Vpf4 = (bytes[pointer] & 0x8) != 0;
            Vpf5 = (bytes[pointer] & 0x4) != 0;
            Lcn = (ushort)(((bytes[pointer] & 0x3) << 8) + bytes[pointer + 1]);
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Service id: {ServiceId}, Lcn: {Lcn}, visible flag: {VisibleServiceFlag}\n";
        }
    }
    public record LogicalChannelNumberDescriptorV2_0x87 : Descriptor
    {
        public List<ChannelList> ChannelLists { get; }
        public LogicalChannelNumberDescriptorV2_0x87(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            int pointer = 2;
            ChannelLists = new List<ChannelList>();

            while (pointer < DescriptorLength)
            {
                var clt = new ChannelList(bytes[pointer..]);
                pointer += clt.ChannelListNameLength + clt.ServiceIdListLength + 6;
                ChannelLists.Add(clt);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";

            foreach (var clt in ChannelLists)
            {
                str += clt.Print(prefixLen + 2);
            }
            return str;
        }
    }
}
