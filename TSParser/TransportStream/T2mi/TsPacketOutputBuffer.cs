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

namespace TSParser.TransportStream.T2mi;

/// <summary>Accumulates de-encapsulated chunks and emits complete 188-byte MPEG-TS packets.</summary>
internal sealed class TsPacketOutputBuffer
{
    private const int TsPacketSize = T2miAccessors.TsPacketSize;
    private readonly List<byte> _pending = new(4096);
    private readonly List<byte> _emitBatch = new(4096);

    public void Reset()
    {
        _pending.Clear();
        _emitBatch.Clear();
    }

    public void Append(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty)
        {
            return;
        }

        for (var i = 0; i < chunk.Length; i++)
        {
            _pending.Add(chunk[i]);
        }
    }

    /// <summary>Extracts aligned 188-byte packets; uses a 5-packet 0x47 pattern only when resync is needed.</summary>
    public void Flush(Action<ReadOnlyMemory<byte>> emit)
    {
        while (_pending.Count >= TsPacketSize)
        {
            if (_pending[0] != TsPacket.SYNC_BYTE)
            {
                var syncOffset = TryFindSyncOffset();
                if (syncOffset < 0)
                {
                    if (_pending.Count > TsPacketSize)
                    {
                        _pending.RemoveRange(0, _pending.Count - (TsPacketSize - 1));
                    }

                    return;
                }

                _pending.RemoveRange(0, syncOffset);
                continue;
            }

            var alignedCount = 0;
            while (alignedCount + TsPacketSize <= _pending.Count
                   && _pending[alignedCount] == TsPacket.SYNC_BYTE)
            {
                alignedCount += TsPacketSize;
            }

            if (alignedCount == 0)
            {
                _pending.RemoveAt(0);
                continue;
            }

            if (alignedCount < TsPacketSize * 5)
            {
                var tail = _pending.Count - alignedCount;
                if (tail > 0 && !CanTrustAlignedRun(alignedCount))
                {
                    return;
                }
            }

            _emitBatch.Clear();
            for (var i = 0; i < alignedCount; i++)
            {
                _emitBatch.Add(_pending[i]);
            }

            _pending.RemoveRange(0, alignedCount);
            emit(_emitBatch.ToArray());
        }
    }

    private bool CanTrustAlignedRun(int alignedBytes)
    {
        if (alignedBytes == TsPacketSize)
        {
            return true;
        }

        return CheckPattern(0, TsPacketSize);
    }

    private int TryFindSyncOffset()
    {
        var limit = Math.Min(_pending.Count, TsPacketSize);
        for (var offset = 0; offset < limit; offset++)
        {
            if (CheckPattern(offset, TsPacketSize))
            {
                return offset;
            }
        }

        return -1;
    }

    private bool CheckPattern(int offset, int packetSize)
    {
        if (_pending.Count < offset + packetSize * 5)
        {
            return false;
        }

        for (var k = 0; k < 5; k++)
        {
            if (_pending[offset + k * packetSize] != TsPacket.SYNC_BYTE)
            {
                return false;
            }
        }

        return true;
    }
}
