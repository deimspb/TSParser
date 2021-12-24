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
using TSParser.DictionariesData;
using TSParser.Service;

namespace TSParser.Tables.DvbTables
{
    public record PMT : Table
    {
        public ushort ProgramNumber { get; }
        public ushort PcrPid { get; }
        public ushort ProgramInfoLength { get; }
        public List<Descriptor> PmtDescriptorList { get; } = default!;
        public List<EsInfo> EsInfoList { get; } = default!;
        public PMT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            if (TableId != 0x02)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {TableId} for PMT table");
                return;
            }

            ProgramNumber = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
            PcrPid = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]) & 0x1FFF);
            ProgramInfoLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[10..]) & 0x0FFF);
            var pointer = 12;
            var descAllocation = $"Table: PMT, Program: {ProgramNumber}, Section number: {SectionNumber}";

            if (ProgramInfoLength > 0)
            {
                PmtDescriptorList = DescriptorFactory.GetDescriptorList(bytes[pointer..(pointer + ProgramInfoLength)], descAllocation);
            }

            pointer += ProgramInfoLength;

            EsInfoList = GetEsInfoList(bytes[pointer..^4]);
        }
        private List<EsInfo> GetEsInfoList(ReadOnlySpan<byte> bytes)
        {

            List<EsInfo> esList = new();
            var pointer = 0;

            while (pointer < bytes.Length)
            {
                var es = new EsInfo(bytes[pointer..],ProgramNumber);
                pointer += es.EsInfoLength + 5;

                esList.Add(es);
            }
            return esList;
        }
        public override string ToString()
        {
            string pmt = $"-=PMT for program number: {ProgramNumber}=-\n";

            pmt+= $"{base.ToString()}";

            pmt += $"   Program number: {ProgramNumber}\n";
            pmt += $"   PCR pid: {PcrPid}\n";
            pmt += $"   Program info length: {ProgramInfoLength}\n";

            if(PmtDescriptorList is not null)
            {
                pmt += $"   PMT descriptor count: {PmtDescriptorList.Count}\n";
                foreach (var desc in PmtDescriptorList)
                {
                    pmt += $"      {desc}\n";
                }
            }

            if(EsInfoList is not null)
            {
                pmt += $"   PMT ES records count: {EsInfoList.Count}\n";
                foreach (var es in EsInfoList)
                {
                    pmt += $"{es}";
                }
            }          
            

            pmt += $"   PMT CRC: 0x{CRC32:X}\n";
            return pmt;
        }
        public virtual bool Equals(PMT? table)
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
    }

    public struct EsInfo
    {
        public byte StreamType { get; }
        public string StreamTypeName => Dictionaries.EsTypeNames[StreamType];
        public ushort ElementaryPid { get; }
        public ushort EsInfoLength { get; }
        public List<Descriptor> EsDescriptorList { get; }
        public EsInfo(ReadOnlySpan<byte> bytes,ushort programId)
        {

            var pointer = 0;
            StreamType = bytes[pointer++];
            ElementaryPid = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x1FFF);
            pointer += 2;
            EsInfoLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            var descAllocation = $"Table: PMT, Program: {programId}, Es pid: {ElementaryPid}";
            EsDescriptorList = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, EsInfoLength), descAllocation);
        }

        public override string ToString()
        {
            string es = $"      ES PID: {ElementaryPid}\n";
            es += $"         Stream type: {StreamType}\n";
            es += $"         Stream type name: {StreamTypeName}\n";
            es += $"         ES info legth: {EsInfoLength}\n";
            es += $"         ES descriptor count: {EsDescriptorList.Count} \n";
            foreach (var desc in EsDescriptorList)
            {
                es += $"{desc}\n";
            }
            return es;
        }
    }
}
