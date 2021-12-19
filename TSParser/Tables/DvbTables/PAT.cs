// Copyright 2021 Eldar Nizamutdinov 
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

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSParser.Service;

namespace TSParser.Tables.DvbTables
{
    public record PAT : Table
    {
        public ushort TransportStreamId { get; }
        public PatRecord[] PatRecords { get; } = null!;
        public PAT(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            if (TableId != 0x00)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {TableId} for PMT");
                return;
            }

            TransportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(4, 2));

            PatRecords = new PatRecord[(SectionLength - 8) / 4];

            for (int i = 0; i < PatRecords.Length; i++)
            {
                ReadOnlySpan<byte> span = bytes[(8 + i * 4)..]; // 12 bytes 
                PatRecords[i] = new PatRecord(span);
            }
        }

        public override string ToString()
        {
            string pat = $"-=PAT=-\n";

            pat += $"{base.ToString()}";            

            pat += $"   Transport stream id: {TransportStreamId} \n";
            foreach (var pr in PatRecords)
            {
                pat += $"      {pr} \n";
            }

            pat += $"   PAT CRC: 0x{CRC32:X}";

            return pat;
        }

        public virtual bool Equals(PAT? pat)
        {
            if (pat == null) return false;
            return CRC32 == pat.CRC32;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)CRC32;
            }
        }
    }

    public record PatRecord
    {
        public ushort ProgramNumber { get; }
        public ushort Pid { get; }
        public PatRecord(ReadOnlySpan<byte> bytes)
        {
            ProgramNumber = BinaryPrimitives.ReadUInt16BigEndian(bytes[0..2]);
            Pid = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]) & 0x1FFF);
        }

        public override string ToString()
        {
            return $"Program number: {ProgramNumber}, Pid: {Pid}";
        }
    }
}
