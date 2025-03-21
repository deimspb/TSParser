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
using TSParser.Service;
using TSParser.Tables.DvbTables;
using TSParser.TransportStream;

namespace TSParser.Tables.DvbTableFactory
{
    internal class AitFactory : TableFactory
    {
        internal event AitReady OnAitReady = null!;
        private AIT m_ait = null!;

        internal AIT Ait
        {
            get => m_ait;
            set => m_ait = value;
        }
        private AIT CurrentAit = null!;
        private uint CurrentCRC32;


        internal override void PushTable(TsPacket tsPacket)
        {            
            AddData(tsPacket);
            if (!IsAllTable) return;
            ParseAit();
        }
        private void ParseAit()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            if (bytes[0] != 0x74)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: 0x{bytes[0]:X} for AIT table");
                return;
            }

            CurrentCRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);            

            if (Ait?.CRC32 == CurrentCRC32) return; //// if we already have ait table and its crc32 equal curent table crc drop it. because it is the same ait

            if (Utils.GetCRC32(bytes[..^4]) != CurrentCRC32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"AIT pid {CurrentPid} CRC incorrect!");
                ResetFactory();
                return;
            }

            CurrentAit = new AIT(bytes, CurrentPid);

            if (Ait != null && Ait.VersionNumber != CurrentAit.VersionNumber)
            {
                Logger.Send(LogStatus.INFO, $"AIT version changed from {Ait.VersionNumber} to {CurrentAit.VersionNumber}");
            }

            Ait = CurrentAit;
            OnAitReady?.Invoke(Ait);
        }
    }
}
