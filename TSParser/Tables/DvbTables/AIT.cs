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
using TSParser.Descriptors;
using TSParser.Service;

namespace TSParser.Tables.DvbTables
{
    public record AIT : Table
    {
        public bool TestApplicationFlag { get; }
        public ushort ApplicationType { get; }
        public ushort CommonDescriptorsLength { get; }
        public List<Descriptor> AitDescriptorsList { get; } = default!;
        public ushort ApplicationLoopLength { get; }
        public List<ApplicationLoop> ApplicationLoops { get; } = default!;

        private ushort m_aitPid;
        public AIT(ReadOnlySpan<byte> bytes, ushort aitPid) : base(bytes)
        {
            m_aitPid = aitPid;

            if (TableId != 0x74)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {TableId} for AIT table");
            }

            TestApplicationFlag = (bytes[3] & 0x80) != 0;
            ApplicationType = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]) & 0x7FFF);
            CommonDescriptorsLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]) & 0x0FFF);
            var pointer = 10;
            var descAllocation = $"Table: AIT, pid: {aitPid}";
            AitDescriptorsList = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer,CommonDescriptorsLength),descAllocation,TableId);
            pointer += CommonDescriptorsLength;
            ApplicationLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;            
            ApplicationLoops = GetAppLoopList(bytes.Slice(pointer, ApplicationLoopLength),aitPid);
            pointer += ApplicationLoopLength;

        }
        public override string ToString()
        {
            string str = $"-=AIT pid: {m_aitPid}=-\n";

            str+=base.ToString();

            str += $"   Test Application Flag: {TestApplicationFlag}\n";
            str += $"   Application Type: {ApplicationType}\n";
            str += $"   Common Descriptors Length: {CommonDescriptorsLength}\n";

            if(AitDescriptorsList is not null)
            {
                str += $"   AIT descriptors count: {AitDescriptorsList.Count}\n";
                foreach(var descriptor in AitDescriptorsList)
                {
                    str += $"      {descriptor}\n";
                }
            }

            str += $"   Application Loop Length: {ApplicationLoopLength}\n";

            if(ApplicationLoops is not null)
            {
                str += $"   Application Loops count: {ApplicationLoops.Count}\n";
                foreach (var loop in ApplicationLoops)
                {
                    str += $"   {loop}\n";
                }
            }

            str += $"   AIT CRC: 0x{CRC32:X}\n";

            return str;
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}-=AIT pid: {m_aitPid}=-\n";

            str += base.Print(prefixLen + 2);

            str += $"{prefix}Test Application Flag: {TestApplicationFlag}\n";
            str += $"{prefix}Application Type: {ApplicationType}\n";
            str += $"{prefix}Common Descriptors Length: {CommonDescriptorsLength}\n";

            if (AitDescriptorsList is not null)
            {
                str += $"{prefix}AIT descriptors count: {AitDescriptorsList.Count}\n";
                foreach (var descriptor in AitDescriptorsList)
                {
                    str += descriptor.Print(prefixLen + 4);
                }
            }

            str += $"{prefix}Application Loop Length: {ApplicationLoopLength}\n";

            if (ApplicationLoops is not null)
            {
                str += $"{prefix}Application Loops count: {ApplicationLoops.Count}\n";
                foreach (var loop in ApplicationLoops)
                {
                    str += loop.Print(prefixLen + 4);
                }
            }

            str += $"{prefix}AIT CRC: 0x{CRC32:X}\n";

            return str;
        }
        public virtual bool Equals(AIT? table)
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
        private List<ApplicationLoop> GetAppLoopList(ReadOnlySpan<byte> bytes, ushort aitPid)
        {
            var items = new List<ApplicationLoop>();
            var pointer = 0;
            while (pointer < bytes.Length)
            {
                var item = new ApplicationLoop(bytes[pointer..],aitPid);
                pointer += item.ApplicationDescriptorsLoopLength + 9;
                items.Add(item);
            }
            return items;
        }
    }
    public struct ApplicationLoop
    {
        public ApplicationIdentifier AppIdentifier { get; } //48 bit
        public byte ApplicationControlCode { get; }//TODO: implement names with table 3 ETSI TS 102 809 v1.3.1
        public ushort ApplicationDescriptorsLoopLength { get; }
        public List<Descriptor> ApplicationLoopDescriptors { get; } = default!;
        public ApplicationLoop(ReadOnlySpan<byte> bytes, ushort aitPid)
        {
            var pointer = 0;
            AppIdentifier = new(bytes.Slice(pointer, 8));
            pointer += 6;
            ApplicationControlCode = bytes[pointer++];
            // reserved 4 bits
            ApplicationDescriptorsLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            var descAllocation = $"Table: AIT, pid: {aitPid}, App identifier: 0x{AppIdentifier.ApplicationId:X}";            
            ApplicationLoopDescriptors = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer,ApplicationDescriptorsLoopLength),descAllocation,0x74); // caller table id 0x74 AIT table
        }
        public override string ToString()
        {
            string str = $"      Application ID: 0x{AppIdentifier.ApplicationId:X}\n";
            str += $"         Organisation ID: 0x{AppIdentifier.OrganisationId:X}\n";
            str += $"         Application Control Code: {ApplicationControlCode}\n";

            if(ApplicationLoopDescriptors is not null)
            {
                str += $"         Application loop descriptors count: {ApplicationLoopDescriptors.Count}\n";
                foreach(var descriptor in ApplicationLoopDescriptors)
                {
                    str += $"         {descriptor}";
                }
            }

            return str;
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Application ID: 0x{AppIdentifier.ApplicationId:X}\n";
            str += $"{prefix}Organisation ID: 0x{AppIdentifier.OrganisationId:X}\n";
            str += $"{prefix}Application Control Code: {ApplicationControlCode}\n";

            if (ApplicationLoopDescriptors is not null)
            {
                str += $"{prefix}Application loop descriptors count: {ApplicationLoopDescriptors.Count}\n";
                foreach (var descriptor in ApplicationLoopDescriptors)
                {
                    str += descriptor.Print(prefixLen + 4);
                }
            }

            return str;
        }

    }
    public struct ApplicationIdentifier
    {
        public uint OrganisationId { get; }
        public ushort ApplicationId { get; }
        public ApplicationIdentifier(ReadOnlySpan<byte> bytes)
        {
            OrganisationId = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            ApplicationId = BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
        }
    }
}
