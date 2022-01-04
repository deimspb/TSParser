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
using TSParser.Tables.DvbTables;
using TSParser.TransportStream;

namespace TSParser.Tables.DvbTableFactory
{
    internal class Scte35Factory : TableFactory
    {
        internal event Scte35Ready OnScte35Ready = null!;
        private SCTE35 scte35 = null!;

        internal SCTE35 Scte35
        {
            get => scte35;
            set => scte35 = value;
        }
        private SCTE35 CurrentScte35=null!;
        private uint CurrentCRC32;
        internal override void PushTable(TsPacket tsPacket)
        {
            AddData(tsPacket);
            if (!IsAllTable) return;
            ParseScte35();
        }

        private void ParseScte35()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            CurrentCRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);            

            if (Scte35?.CRC32 == CurrentCRC32) return; //if we already have scte35 table and its crc32 equal curent table crc drop it. because it is the same scte35

            if (Utils.GetCRC32(bytes[..^4]) != CurrentCRC32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"SCTE35 pid {CurrentPid} CRC incorrect!");
                ResetFactory();
                return;
            }

            CurrentScte35 = new SCTE35(bytes, CurrentPid);

            Scte35 = CurrentScte35;
            OnScte35Ready?.Invoke(Scte35);
        }
    }
}
