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

namespace TSParser.Tables.DvbTableFactory;

internal class EewsFactory : TableFactory
{
    internal event EewsReady OnEewsReady = null!;

    private EEWS m_eews = null!;

    internal EEWS Eews
    {
        get { return m_eews; }
        set { m_eews = value; }
    }

    private EEWS CurrentEews = null!;
    private uint CurrentCRC32;

    internal override void PushTable(TsPacket tsPacket)
    {
        AddData(tsPacket);
        if (!IsAllTable) return;
        ParseEews();
    }

    internal void ParseEews()
    {
        ReadOnlySpan<byte> bytes = TableData.AsSpan();

        if (bytes[0] != 0x94 && bytes[0] != 0x95)
        {
            Logger.Send(LogStatus.ETSI, $"Invalid table id: {bytes[0]} for EEWS table");
            return;
        }
        CurrentCRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
        if (Eews?.CRC32 == CurrentCRC32) return; // if we already have ews table and its crc32 equal curent table crc drop it. because it is the same ews
        if (Utils.GetCRC32(bytes[..^4]) != CurrentCRC32) // drop invalid ts packet
        {
            Logger.Send(LogStatus.ETSI, $"EEWS pid {CurrentPid} CRC incorrect!");
            ResetFactory();
            return;
        }
        Eews = new EEWS(bytes, CurrentPid);

        OnEewsReady?.Invoke(Eews);
    }
}
