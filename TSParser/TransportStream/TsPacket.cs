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

namespace TSParser.TransportStream
{
    public readonly struct TsPacket
    {       

        public const byte SYNC_BYTE = 0x47;
        public readonly bool TransportErrorIndicator { get; }
        public readonly bool PayloadUnitStartIndicator { get; } = default;
        public readonly bool TransportPriority { get; } = default;
        public readonly ushort Pid { get; } = 0xFFFF; // PID min value 0, max value 0x1FFF
        public readonly byte TransportScramblingControl { get; } = default;
        public readonly byte AdaptationFieldControl { get; } = default;
        public readonly byte ContinuityCounter { get; } = default;
        public readonly bool HasPayload { get; } = default;
        public readonly bool HasAdaptationField { get; } = default;
        public readonly PesHeader Pes_header { get; } = default;
        public readonly bool HasPesHeader { get; } = default;
        public readonly AdaptationField Adaptation_field { get; } = default;
        public readonly byte[] Payload { get; }       
        public readonly ulong PacketNumber { get; } = default;
        public readonly byte[] PacketHeader { get; }

        internal TsPacket(ReadOnlySpan<byte> bytes, ulong packetCounter)
        {
            var pointer = 0;
            var pid = 0xFFFF;
            PacketNumber = packetCounter;
            PacketHeader = new byte[4];
            bytes[0..4].CopyTo(PacketHeader);
            try
            {
                if (bytes[pointer++] != SYNC_BYTE)
                {
                    Logger.Send(LogStatus.ETSI, $"Sync Loss in packet: {PacketNumber}");
                    throw new Exception("Invalid sync byte!");
                }

                TransportErrorIndicator = ((bytes[1] & 0x80) >> 7) != 0;
                pid = Pid = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2)) & 0x1FFF);

                if (TransportErrorIndicator || Pid == 0x1FFF) // if tei or null packet, copy payload and return
                {
                    Payload = new byte[184];
                    bytes.Slice(4).CopyTo(Payload);
                    return;
                }

                PayloadUnitStartIndicator = ((bytes[1] & 0x40) >> 6) != 0;
                TransportPriority = ((bytes[1] & 0x20) >> 5) != 0;
                TransportScramblingControl = ((byte)((bytes[3] & 0xC0) >> 6));
                AdaptationFieldControl = ((byte)((bytes[3] & 0x30) >> 4));
                ContinuityCounter = (byte)(bytes[3] & 0x0F);
                HasPayload = (bytes[3] & 0x10) != 0;
                HasAdaptationField = (bytes[3] & 0x20) != 0;
                pointer = 4;

                if (HasAdaptationField)
                {
                    Adaptation_field = new AdaptationField(bytes.Slice(pointer), out int outPointer);
                    pointer += outPointer;
                }

                if (PayloadUnitStartIndicator)
                {
                    if (188 - pointer > 6 && (BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(pointer - 1, 4)) & 0x00FFFFFF) == 0x000001)
                    {
                        HasPesHeader = true;
                        Pes_header = new PesHeader(bytes.Slice(pointer + 3), out int outPointer);
                        pointer += outPointer;
                    }
                }

                var payloadSize = 188 - pointer;

                if (payloadSize > 1 && payloadSize <= 188 - 4)
                {
                    Payload = new byte[payloadSize];
                    bytes.Slice(pointer).CopyTo(Payload);
                }
                else
                {
                    Payload = Array.Empty<byte>();
                }

            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.EXCEPTION, $"Exception while parsing packet: {PacketNumber}, pid: {pid}, {ex}");
                throw new Exception($"Exception while parsing packet: {PacketNumber}, pid: {pid}, {ex}");
            }
        }
    }
}
