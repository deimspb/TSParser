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

using TSParser.Service;

namespace TSParser.TransportStream.T2mi;

/// <summary>Reassembles T2-MI packets from MPEG-TS payloads.</summary>
public sealed class T2miPacketAssembler
{
    private const int BufferSize = 0x3000;
    private const int MaxPacketSize = T2miAccessors.T2miPacketHeaderSize + 8 * 1024 + 4;

    private readonly byte[] _building = new byte[BufferSize];
    private int _writtenSoFar;
    private ushort _t2miPacketCount = 0xFFFF;
    private byte _tsPacketCc;
    private byte _t2miFrameIndex = 0xFF;
    private bool _dvbt2Timestamp = true;
    private bool _t2miL1CurrentData = true;
    private ushort _sourcePid;
    private ulong _packetNumber;

    internal bool HasDvbt2Timestamp => _dvbt2Timestamp;
    internal bool HasL1CurrentData => _t2miL1CurrentData;

    public event Action<T2miPacket>? PacketReady;

    public void Reset()
    {
        _writtenSoFar = 0;
        _t2miPacketCount = 0xFFFF;
        _tsPacketCc = 0;
        _t2miFrameIndex = 0xFF;
        _dvbt2Timestamp = true;
        _t2miL1CurrentData = true;
    }

    public void PushPacket(ReadOnlySpan<byte> tsPacket, ushort sourcePid = 0, ulong packetNumber = 0)
    {
        if (tsPacket.Length != T2miAccessors.TsPacketSize)
        {
            throw new ArgumentException($"Expected {T2miAccessors.TsPacketSize} bytes.", nameof(tsPacket));
        }

        if (T2miAccessors.TsSyncByte(tsPacket) != TsPacket.SYNC_BYTE)
        {
            return;
        }

        _sourcePid = sourcePid != 0 ? sourcePid : T2miAccessors.TsPid(tsPacket);
        _packetNumber = packetNumber;

        var actualCc = T2miAccessors.TsContinuityCounter(tsPacket);
        if (_writtenSoFar != 0)
        {
            var expectedCc = (byte)((_tsPacketCc + 1) & 0x0F);
            if (expectedCc != actualCc)
            {
                if (actualCc != _tsPacketCc || T2miAccessors.TsHasPayload(tsPacket))
                {
                    SignalDiscontinuityIfPossible();
                    _writtenSoFar = 0;
                }
            }
            else if (!T2miAccessors.TsPayloadUnitStartIndicator(tsPacket))
            {
                AppendTsPayload(tsPacket);
            }

            _tsPacketCc = actualCc;
        }

        if (T2miAccessors.TsPayloadUnitStartIndicator(tsPacket))
        {
            StartNewT2miFromTs(tsPacket, actualCc);
        }
    }

    private void StartNewT2miFromTs(ReadOnlySpan<byte> tsPacket, byte actualCc)
    {
        var payload = T2miAccessors.TsPayload(tsPacket);
        if (payload.IsEmpty)
        {
            SignalDiscontinuityIfPossible();
            _writtenSoFar = 0;
            return;
        }

        var pointer = payload[0];
        var dataStart = pointer + 1;
        if (dataStart >= payload.Length)
        {
            SignalDiscontinuityIfPossible();
            _writtenSoFar = 0;
            return;
        }

        if (_writtenSoFar > 0 && pointer > 0)
        {
            CopyToBuffer(payload.Slice(1, pointer), _writtenSoFar);
            DeliverIfPacketIsCompleted();
        }
        else if (_writtenSoFar > 0)
        {
            SignalDiscontinuityIfPossible();
            _writtenSoFar = 0;
        }

        if (_writtenSoFar > 0)
        {
            SignalDiscontinuityIfPossible();
            _writtenSoFar = 0;
        }

        var t2miChunk = payload.Slice(dataStart);
        CopyToBuffer(t2miChunk, atOffset: 0);
        _tsPacketCc = actualCc;
        DeliverIfPacketIsCompleted();
    }

    private void AppendTsPayload(ReadOnlySpan<byte> tsPacket)
    {
        var payload = T2miAccessors.TsPayload(tsPacket);
        if (payload.IsEmpty)
        {
            return;
        }

        ReadOnlySpan<byte> t2miChunk;
        if (!T2miAccessors.TsPayloadUnitStartIndicator(tsPacket))
        {
            t2miChunk = payload;
        }
        else
        {
            var pointer = payload[0];
            if (pointer >= payload.Length)
            {
                SignalDiscontinuityIfPossible();
                _writtenSoFar = 0;
                return;
            }

            t2miChunk = payload.Slice(pointer + 1);
        }

        if (t2miChunk.IsEmpty || t2miChunk.Length >= 185)
        {
            SignalDiscontinuityIfPossible();
            _writtenSoFar = 0;
            return;
        }

        CopyToBuffer(t2miChunk, _writtenSoFar);
        DeliverIfPacketIsCompleted();
    }

    private void CopyToBuffer(ReadOnlySpan<byte> data, int atOffset)
    {
        var offset = atOffset;
        var srcOffset = 0;
        while (srcOffset < data.Length && offset < BufferSize)
        {
            var remainingInPacket = GetRemainingT2miBytes(offset);
            var toWrite = Math.Min(Math.Min(data.Length - srcOffset, BufferSize - offset), remainingInPacket);
            if (toWrite <= 0)
            {
                break;
            }

            data.Slice(srcOffset, toWrite).CopyTo(_building.AsSpan(offset));
            offset += toWrite;
            srcOffset += toWrite;
        }

        _writtenSoFar = offset;
    }

    private int GetRemainingT2miBytes(int atOffset)
    {
        if (atOffset < T2miAccessors.T2miPacketHeaderSize)
        {
            return T2miAccessors.T2miPacketHeaderSize - atOffset;
        }

        var expected = T2miAccessors.T2miPacketSizeBytes(_building.AsSpan(0, atOffset));
        if (expected > MaxPacketSize)
        {
            return int.MaxValue;
        }

        return Math.Max(0, expected - atOffset);
    }

    private void DeliverIfPacketIsCompleted()
    {
        while (_writtenSoFar >= 10)
        {
            var building = _building.AsSpan(0, _writtenSoFar);
            var t2miPacketSize = T2miAccessors.T2miPacketSizeBytes(building);
            if (t2miPacketSize > MaxPacketSize || t2miPacketSize > _writtenSoFar)
            {
                break;
            }

            var packetType = (T2miPacketType)T2miAccessors.T2miType(building);
            var buildingPacketCount = T2miAccessors.T2miCount(building);
            _t2miPacketCount = buildingPacketCount;

            var superframeIndex = T2miAccessors.T2miSuperframeIndex(building);
            if (superframeIndex != _t2miFrameIndex)
            {
                _t2miFrameIndex = superframeIndex;
                _dvbt2Timestamp = false;
                _t2miL1CurrentData = false;
            }

            T2miPacket? completed = null;
            if (packetType == T2miPacketType.BasebandFrame)
            {
                var computedCrc = Utils.GetCRC32(building.Slice(0, t2miPacketSize - 4));
                var packetCrc = T2miAccessors.T2miCrc32(building);
                var crcValid = computedCrc == packetCrc;
                if (!crcValid)
                {
                    Logger.Send(LogStatus.ETSI,
                        $"T2-MI baseband frame CRC mismatch: expected {computedCrc:X8}, got {packetCrc:X8}");
                }

                var payloadBits = T2miAccessors.T2miType00PayloadLengthBits(building);
                var payloadSize = (payloadBits + 7) / 8;
                if (payloadSize >= 10)
                {
                    completed = BuildPacket(building, packetType, crcValid, payloadSize);
                }
            }
            else
            {
                if (packetType == T2miPacketType.L1Current)
                {
                    _t2miL1CurrentData = true;
                }

                if (packetType == T2miPacketType.DvbT2Timestamp)
                {
                    _dvbt2Timestamp = true;
                }

                var payloadBytes = (T2miAccessors.T2miPayloadLengthBits(building) + 7) / 8;
                completed = BuildPacket(building, packetType, crcValid: true, payloadBytes);
            }

            if (completed != null)
            {
                PacketReady?.Invoke(completed);
            }

            if (_writtenSoFar > t2miPacketSize)
            {
                var bytesLeft = _writtenSoFar - t2miPacketSize;
                building.Slice(t2miPacketSize, bytesLeft).CopyTo(_building);
                _writtenSoFar = bytesLeft;
            }
            else
            {
                _writtenSoFar = 0;
                break;
            }
        }
    }

    private T2miPacket BuildPacket(ReadOnlySpan<byte> building, T2miPacketType packetType, bool crcValid, int payloadByteCount)
    {
        byte[] payload;
        byte? plpId = null;
        byte? frameIndex = null;
        bool? intlFrameStart = null;

        if (packetType == T2miPacketType.BasebandFrame)
        {
            var bb = T2miAccessors.T2miType00Payload(building);
            payload = bb.Slice(0, Math.Min(payloadByteCount, bb.Length)).ToArray();
            plpId = T2miAccessors.T2miType00PlpId(building);
            frameIndex = T2miAccessors.T2miType00FrameIndex(building);
            intlFrameStart = T2miAccessors.T2miType00IntlFrameStart(building);
        }
        else
        {
            var raw = T2miAccessors.T2miPayload(building);
            payload = raw.Slice(0, Math.Min(payloadByteCount, raw.Length)).ToArray();
        }

        return new T2miPacket
        {
            PacketType = packetType,
            SuperframeIndex = T2miAccessors.T2miSuperframeIndex(building),
            StreamId = T2miAccessors.T2miStreamId(building),
            PlpId = plpId,
            FrameIndex = frameIndex,
            IntlFrameStart = intlFrameStart,
            Payload = payload,
            Crc32Valid = crcValid,
            SourcePid = _sourcePid,
            PacketNumber = _packetNumber,
            PacketCount = T2miAccessors.T2miCount(building),
            Rfu = T2miAccessors.T2miRfu(building),
        };
    }

    private void SignalDiscontinuityIfPossible()
    {
        if (_writtenSoFar < 8)
        {
            return;
        }

        var building = _building.AsSpan(0, _writtenSoFar);
        if ((T2miPacketType)T2miAccessors.T2miType(building) == T2miPacketType.BasebandFrame)
        {
            _ = T2miAccessors.T2miType00PlpId(building);
        }
    }
}
