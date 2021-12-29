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
using TSParser.Tables.Mip;
using TSParser.TransportStream;

namespace TSParser.Tables.DvbTableFactory
{
    internal class MipFactory : TableFactory
    {
        internal event MipReady OnMipReady = null!;
        private MIP m_mip = null!;

        internal MIP Mip
        {
            get => m_mip;
            set => m_mip = value;
        }

        private MIP CurrentMip = null!;
        private uint CurrentCRC32;
        internal override void PushTable(TsPacket tsPacket)
        {
            var packetlength = tsPacket.Payload[1];

            ReadOnlySpan<byte> bytes = tsPacket.Payload[0..(packetlength + 2)];
            //just to check incoming CRC
            //!!! MIP table CRC32 include ts packet header!!!
            byte[] fulltable = new byte[4 + packetlength + 2];
            tsPacket.PacketHeader.CopyTo(fulltable, 0);
            tsPacket.Payload[0..(packetlength + 2)].CopyTo(fulltable, 4);

            CurrentCRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);

            if (Utils.GetCRC32(fulltable[..^4]) != CurrentCRC32) // drop invalid ts packet
            {
                Logger.Send(LogStatus.ETSI, $"MIP CRC incorrect!");
                return;
            }

            if (Mip?.CRC32 == CurrentCRC32) return;

            CurrentMip = new MIP(bytes);

            Mip = CurrentMip;
            OnMipReady?.Invoke(Mip);
        }

    }
}
