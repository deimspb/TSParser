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

using TSParser.Service;

namespace TSParser.TransportStream
{
    internal class TsPacketFactory
    {
        private ulong m_packetCounter = 0;
        private uint m_syncLoss = 0;

        internal TsPacket[] GetTsPackets(ReadOnlySpan<byte> bytes, int packetLength)
        {
            if (packetLength < 188)
            {
                Logger.Send(LogStatus.Exception, $"Byte array is too small");
                throw new Exception("Invalid packet length!");
            }

            if (bytes.Length % packetLength > 0)
            {
                Logger.Send(LogStatus.Warning, $"Invalid array length! array.len % packet len > 0 {bytes.Length}");                
            }

            var packetCount = bytes.Length / packetLength;
            var tsPackets = new TsPacket[packetCount];

            for (int i = 0; i < packetCount; i++)
            {
                if (bytes[i * packetLength] == TsPacket.SYNC_BYTE)
                {
                    tsPackets[i] = GetTsPacket(bytes.Slice(i * packetLength, packetLength), packetLength);
                    m_packetCounter++;
                }
                else
                {
                    Logger.Send(LogStatus.ETSI, $"Sync loss after packet: {packetCount}");                    
                    m_syncLoss++;
                }
            }

            return tsPackets;
        }
        internal TsPacket GetTsPacket(ReadOnlySpan<byte> data, int packetLength)
        {
            ReadOnlySpan<byte> bytes;

            if (packetLength == 204)
            {
                bytes = data[..^16]; // TODO: implement FEC or TimeStamp
                packetLength = 188;
            }
            else
            {
                bytes = data;
            }

            try
            {
                return new TsPacket(bytes, m_packetCounter);
            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.Exception, $"");
                return default(TsPacket);
            }
        }
    }
}
