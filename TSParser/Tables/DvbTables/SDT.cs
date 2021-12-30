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
using TSParser.Descriptors;
using TSParser.Service;

namespace TSParser.Tables.DvbTables
{
    public record SDT : Table
    {
        public ushort TransportStreamId { get; }
        public ushort OriginalNetworkId { get; }
        public List<ServiceDescriptionItem> SdtItemsList { get; } = null!;

        public SDT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            TransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
            OriginalNetworkId = BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]);
            //byte 10 reserved
            SdtItemsList = GetSdtItemList(bytes[11..^4]);
        }
        private List<ServiceDescriptionItem> GetSdtItemList(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            List<ServiceDescriptionItem> items = new List<ServiceDescriptionItem>();
            while (pointer < bytes.Length)
            {
                ServiceDescriptionItem item = new(bytes[pointer..],TransportStreamId);
                pointer += item.DescriptorLoopLength + 5;
                items.Add(item);
            }
            return items;
        }
        public virtual bool Equals(SDT? table)
        {
            if (table == null) return false;

            return CRC32 == table.CRC32;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return (int)CRC32;
            }
        }

        //public override string ToString()
        //{
        //    string sdt = $"-=SDT=-\n";

        //    sdt += base.ToString();

        //    sdt += $"   Transport stream id: {TransportStreamId}\n";
        //    sdt += $"   Original network id: {OriginalNetworkId}\n";

        //    if (SdtItemsList != null)
        //    {
        //        sdt += $"   SDT item list count: {SdtItemsList.Count}\n";
        //        foreach (var item in SdtItemsList)
        //        {
        //            sdt += $"{item}\n";
        //        }
        //    }


        //    sdt += $"   SDT CRC 0x{CRC32:X}\n";
        //    return sdt;
        //}
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string sdt = $"{headerPrefix}-=SDT=-\n";

            sdt += base.Print(prefixLen +2);

            sdt += $"{prefix}Transport stream id: {TransportStreamId}\n";
            sdt += $"{prefix}Original network id: {OriginalNetworkId}\n";

            if (SdtItemsList != null)
            {
                sdt += $"{prefix}SDT item list count: {SdtItemsList.Count}\n";
                foreach (var item in SdtItemsList)
                {
                    sdt += item.Print(prefixLen +4);
                }
            }


            sdt += $"{prefix}SDT CRC32: 0x{CRC32:X}\n";
            return sdt;
        }
    }

    public struct ServiceDescriptionItem
    {
        public ushort ServiceId { get; } = default;
        public bool EitScheduleFlag { get; } = default;
        public bool EitPresentFollowingFlag { get; } = default;
        public byte RunningStatus { get; } = default;
        public bool FreeCAMode { get; } = default;
        public ushort DescriptorLoopLength { get; } = default;
        public List<Descriptor> SdtItemDescriptorList { get; } = null!;
        public ServiceDescriptionItem(ReadOnlySpan<byte> bytes, ushort tsId)
        {
            ServiceId = BinaryPrimitives.ReadUInt16BigEndian(bytes[0..]);
            EitScheduleFlag = (bytes[2] & 0x2) != 0;
            EitPresentFollowingFlag = (bytes[2] & 0x1) != 0;
            RunningStatus = (byte)(bytes[3] >> 5);
            FreeCAMode = (bytes[3] & 0x10) != 0;
            DescriptorLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]) & 0x0FFF);
            var pointer = 5;
            var descAllocation = $"Table: SDT, Ts id: {tsId}, Service id: {ServiceId}";
            SdtItemDescriptorList = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, DescriptorLoopLength), descAllocation);
        }
        public override string ToString()
        {
            string sdtItem = $"      SDT item\n";
            sdtItem += $"         Service id: {ServiceId}\n";

            sdtItem += $"         EIT schedule flag: {EitScheduleFlag}\n";
            sdtItem += $"         EIT present following flag: {EitPresentFollowingFlag}\n";
            sdtItem += $"         Running status: {RunningStatus}\n";
            sdtItem += $"         Free CA mode: {FreeCAMode}\n";
            sdtItem += $"         Descriptor loop length: {DescriptorLoopLength}\n";

            sdtItem += $"         SDT item descriptor count: {SdtItemDescriptorList.Count}\n";
            foreach (var desc in SdtItemDescriptorList)
            {
                sdtItem += $"            {desc}";
            }



            return sdtItem;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string sdtItem = $"{headerPrefix}SDT item\n";
            sdtItem += $"{prefix}Service id: {ServiceId}\n";

            sdtItem += $"{prefix}EIT schedule flag: {EitScheduleFlag}\n";
            sdtItem += $"{prefix}EIT present following flag: {EitPresentFollowingFlag}\n";
            sdtItem += $"{prefix}Running status: {RunningStatus}\n";
            sdtItem += $"{prefix}Free CA mode: {FreeCAMode}\n";
            sdtItem += $"{prefix}Descriptor loop length: {DescriptorLoopLength}\n";

            sdtItem += $"{prefix}SDT item descriptor count: {SdtItemDescriptorList.Count}\n";
            foreach (var desc in SdtItemDescriptorList)
            {
                sdtItem += desc.Print(prefixLen +4);
            }



            return sdtItem;
        }
    }
}
