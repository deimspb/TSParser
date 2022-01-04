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
    internal class PatFactory : TableFactory
    {
        internal event PatReady OnPatReady = null!;
        private PAT m_pat = null!;
        internal PAT Pat
        {
            get { return m_pat; }
            private set { m_pat = value; }
        }

        private PAT CurrentPat = null!;
        private uint CurrentCRC32;
        
        internal override void PushTable(TsPacket tsPacket)
        {
            AddData(tsPacket);
            if (!IsAllTable) return;
            ParsePat();
        }
        private void ParsePat()
        {
            ReadOnlySpan<byte> bytes = TableData.AsSpan();

            if (bytes[0] != 0x00)
            {
                Logger.Send(LogStatus.ETSI, $"Invalid table id: {bytes[0]} for PAT");
                return;
            }

            CurrentCRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);           

            if (Pat?.CRC32 == CurrentCRC32) return; // if we already have pat table and its crc32 equal curent table crc drop it. because it is the same pat            

            if (Utils.GetCRC32(bytes[..^4]) != CurrentCRC32) //
            {
                Logger.Send(LogStatus.ETSI, $"PAT CRC incorrect!");
                ResetFactory();
                return;
            }

            CurrentPat = new PAT(bytes);            

            if (Pat != null && Pat.VersionNumber != CurrentPat.VersionNumber)
            {
                Logger.Send(LogStatus.INFO, $"Pat version changed from {Pat.VersionNumber} to {CurrentPat.VersionNumber}");
            }            

            Pat = CurrentPat;
            OnPatReady?.Invoke(Pat);

        }
    }
}
