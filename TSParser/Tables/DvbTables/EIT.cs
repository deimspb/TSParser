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
    public record EIT : Table
    {
        public ushort ServiceId { get; }

        public ushort TransportStreamId { get; }

        public ushort OriginalNetworkId { get; }

        public byte SegmentLastSectionNumber { get; }

        public byte LastTableId { get; }

        public List<Event> EventList { get; }
        public EIT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            if (TableId != 0x4F && TableId != 0x4E && !(0x50 <= TableId && TableId <= 0x5F) && !(0x60 <= TableId && TableId <= 0x6F)) // check eit table id
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {TableId} for EIT table");                
            }

            ServiceId = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
            TransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]);
            OriginalNetworkId = BinaryPrimitives.ReadUInt16BigEndian(bytes[10..]);
            SegmentLastSectionNumber = bytes[12];
            LastTableId = bytes[13];

            var pointer = 14;                   

            EventList = GetEvents(bytes[pointer..^4]);
        }
        private List<Event> GetEvents(ReadOnlySpan<byte> bytes)
        {
            List<Event> events = new List<Event>();
            var pointer = 0;
            while (pointer < bytes.Length)
            {
                Event evt = new(bytes[pointer..],ServiceId);
                pointer += evt.DescriptorLoopLength + 12;
                events.Add(evt);
            }

            return events;
        }
        public override string ToString()
        {
            var eit = $"-=EIT=-\n";

            eit += $"   Service id: {ServiceId}\n";

            eit += $"{base.ToString()}";             

            eit += $"   Transport stream id: {TransportStreamId}\n";
            eit += $"   Original network id: {OriginalNetworkId}\n";
            eit += $"   Segment last section number: {SegmentLastSectionNumber}\n";
            eit += $"   Last table id: {LastTableId}\n";

            if(EventList != null)
            {
                eit += $"   Event List count: {EventList.Count}\n";
                foreach (var ev in EventList)
                {
                    eit += $"      {ev}\n";
                }
            }            
            eit += $"   CRC: 0x{CRC32:X}\n";

            return eit;
        }

        public virtual bool Equals(EIT? table)
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

    public struct Event
    {
        public ushort EventId { get; }        
        public DateTime StartDateTime { get; } //TODO: change to ulong       
        
        public TimeSpan DurationTimeSpan { get; }

        public byte RunningStatus { get; }

        public bool FreeCAmode { get; }

        public ushort DescriptorLoopLength { get; }

        public List<Descriptor> EventDescriptors { get; }
        public Event(ReadOnlySpan<byte> bytes,ushort serviceId)
        {
            var pointer = 0;
            EventId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            StartDateTime = Utils.GetDateTimeFromMJD_UTC(bytes.Slice(pointer, 5));
            pointer += 5;
            DurationTimeSpan = Utils.GetDuration(bytes.Slice(pointer, 3));
            pointer += 3;
            RunningStatus = (byte)(bytes[pointer] >> 5);
            FreeCAmode = (bytes[pointer] & 0x10) != 0;
            DescriptorLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
            pointer += 2;
            var allocation = $"Table: EIT, Service id: {serviceId}, Event id: {EventId}";
            EventDescriptors = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, DescriptorLoopLength), allocation);

        }

        public override string ToString()
        {
            var evnt = $"      Event id: {EventId}\n";
            evnt += $"      Event start time: {StartDateTime}\n";
            evnt += $"      Event duration: {DurationTimeSpan}\n";
            evnt += $"      Running status: {RunningStatus}\n";
            evnt += $"      Free CA mode: {FreeCAmode}\n";
            evnt += $"      Descriptor loop length: {DescriptorLoopLength}\n";
            foreach (var desc in EventDescriptors)
            {
                evnt += $"         {desc}\n";
            }
            return evnt;
        }
    }
}
