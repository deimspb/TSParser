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

namespace TSParser.TransportStream;

internal static class TsPacketHeader
{
    public static bool TryReadPid(ReadOnlySpan<byte> transportPacket, out ushort pid)
    {
        pid = 0xFFFF;

        if (transportPacket.Length < 4 || transportPacket[0] != TsPacket.SYNC_BYTE)
        {
            return false;
        }

        pid = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(transportPacket.Slice(1, 2)) & 0x1FFF);
        return true;
    }

    public static ReadOnlySpan<byte> GetTransportPacketSpan(ReadOnlySpan<byte> data, int packetLength)
    {
        if (packetLength == 204)
        {
            return data.Length >= 188 ? data[..188] : data;
        }

        return data.Length >= packetLength ? data[..packetLength] : data;
    }
}
