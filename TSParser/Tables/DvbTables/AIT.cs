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
    public record AIT : Table
    {
        public bool TestApplicationFlag { get; }
        public ushort ApplicationType { get; }
        public ushort CommonDescriptorsLength { get; }
        public List<Descriptor> AitDescriptorsList { get; } = default!;
        public ushort ApplicationLoopLength { get; }
        public List<ApplicationLoop> ApplicationLoops { get; } = default!;

        public override ushort TablePid { get; }
        public AIT(ReadOnlySpan<byte> bytes, ushort aitPid) : this(bytes)
        {
            TablePid = aitPid;            
        }
        public AIT(ReadOnlySpan<byte> bytes) : base(bytes)
        {    
            TestApplicationFlag = (bytes[3] & 0x80) != 0;
            ApplicationType = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]) & 0x7FFF);
            CommonDescriptorsLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]) & 0x0FFF);
            var pointer = 10;
            var descAllocation = $"Table: AIT, pid: {TablePid}";
            AitDescriptorsList = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer,CommonDescriptorsLength),descAllocation,TableId);
            pointer += CommonDescriptorsLength;
            ApplicationLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;            
            ApplicationLoops = GetAppLoopList(bytes.Slice(pointer, ApplicationLoopLength), TablePid);      
        }        
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}-=AIT pid: {TablePid}=-\n";

            str += base.Print(prefixLen + 2);

            str += $"{prefix}Test Application Flag: {TestApplicationFlag}\n";
            str += $"{prefix}Application Type: {ApplicationType}\n";
            str += $"{prefix}Common Descriptors Length: {CommonDescriptorsLength}\n";

            if (CommonDescriptorsLength > 0)
            {
                str += $"{prefix}AIT descriptors count: {AitDescriptorsList.Count}\n";
                foreach (var descriptor in AitDescriptorsList)
                {
                    str += descriptor.Print(prefixLen + 4);
                }
            }

            str += $"{prefix}Application Loop Length: {ApplicationLoopLength}\n";

            if (ApplicationLoopLength > 0)
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
        public byte ApplicationControlCode { get; }
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
        
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Application ID: 0x{AppIdentifier.ApplicationId:X}\n";
            str += $"{prefix}Organisation ID: 0x{AppIdentifier.OrganisationId:X}\n";
            str += $"{prefix}Application Control Code: {GetApplicationCodeName(ApplicationControlCode)}\n";

            if (ApplicationLoopDescriptors?.Count > 0)
            {
                str += $"{prefix}Application loop descriptors count: {ApplicationLoopDescriptors.Count}\n";
                foreach (var descriptor in ApplicationLoopDescriptors)
                {
                    str += descriptor.Print(prefixLen + 4);
                }
            }

            return str;
        }
        private static string GetApplicationCodeName(byte bt)
        {
            return bt switch
            {
                0x00 => "reserved_future_use",
                0x01 => "AUTOSTART",
                0x02 => "PRESENT",
                0x03 => "DESTROY",
                0x04 => "KILL",
                0x05 => "PREFETCH",
                0x06 => "REMOTE",
                0x07 => "DISABLED",
                0x08 => "PLAYBACK_AUTOSTART",
                _ => "reserved_future_use"
            };
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
