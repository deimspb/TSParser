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

using TSParser.TransportStream;

namespace TSParser.Input;

internal static class TsPacketLengthDetector
{
    public static int GetPacketLength(ReadOnlySpan<byte> byteArray, out int syncByteOffset)
    {
        syncByteOffset = -1;

        for (var i = 0; i < byteArray.Length - 3 * 204; i++)
        {
            if (byteArray[i] == TsPacket.SYNC_BYTE &&
                byteArray[204 + i] == TsPacket.SYNC_BYTE &&
                byteArray[204 * 2 + i] == TsPacket.SYNC_BYTE &&
                byteArray[3 * 204 + i] == TsPacket.SYNC_BYTE)
            {
                syncByteOffset = i;
                return 204;
            }

            if (byteArray[i] == TsPacket.SYNC_BYTE &&
                byteArray[188 + i] == TsPacket.SYNC_BYTE &&
                byteArray[188 * 2 + i] == TsPacket.SYNC_BYTE &&
                byteArray[3 * 188 + i] == TsPacket.SYNC_BYTE)
            {
                syncByteOffset = i;
                return 188;
            }
        }

        return 0;
    }

    public static bool TryResolveUdpTsPacketLength(int datagramByteCount, out int packetLength)
    {
        packetLength = 0;

        if (datagramByteCount < 188)
        {
            return false;
        }

        var supports188 = datagramByteCount % 188 == 0;
        var supports204 = datagramByteCount % 204 == 0;

        if (supports188 == supports204)
        {
            return false;
        }

        packetLength = supports188 ? 188 : 204;
        return true;
    }
}
