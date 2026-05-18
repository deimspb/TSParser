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

using TSParser.Diagnostics;
using TSParser.Service;

namespace TSParser.TransportStream.T2mi;

/// <summary>Extracts MPEG-TS packets from a DVB-T2 baseband frame payload (ported from sl-demux <c>BBFrameStripper</c>).</summary>
public sealed class BbFrameStripper
{
    private const int MaxPartialBufferSize = 214;
    private static readonly byte[] NullPacketHeader = { 0x47, 0x1F, 0xFF, 0x10 };

    private readonly byte[] _partialBuffer = new byte[MaxPartialBufferSize];
    private readonly TsPacketOutputBuffer _output = new();
    private int _writtenSoFar;

    public byte PlpId { get; }

    /// <summary>Raised with one or more complete 188-byte TS packets. Memory is valid only for the duration of the callback.</summary>
    public event Action<ReadOnlyMemory<byte>>? TsPacketsReady;

    public BbFrameStripper(byte plpId)
    {
        PlpId = plpId;
    }

    public void Reset()
    {
        _writtenSoFar = 0;
        _output.Reset();
    }

    public void Receive(ReadOnlySpan<byte> data)
    {
        // #region agent log
        T2miDebugCounters.BbReceiveCalls++;
        // #endregion

        if (data.Length <= BbHeader.Size)
        {
            return;
        }

        var header = new BbHeader(data);
        var computedCrc = Utils.GetCRC8(data.Slice(0, 9));
        var modeByte = header.Mode;
        var mode = 0;

        if (computedCrc != modeByte)
        {
            if (computedCrc == (byte)(modeByte ^ 1))
            {
                mode = 1;
            }
            else
            {
                // #region agent log
                T2miDebugCounters.BbRejectedHeader++;
                // #endregion
                Logger.Send(LogStatus.ETSI,
                    $"BBHEADER CRC8 mismatch: expected {computedCrc:X2} or {computedCrc ^ 1:X2}, got {modeByte:X2} (PLP {PlpId})");
                return;
            }
        }

        if (header.TsGs != 3)
        {
            // #region agent log
            T2miDebugCounters.BbRejectedHeader++;
            // #endregion
            Logger.Send(LogStatus.ETSI,
                $"BBHEADER TS/GS must be 3 for MPEG-TS in GSE, got {header.TsGs} (PLP {PlpId})");
            return;
        }

        if (header.Dfl == 0)
        {
            // #region agent log
            T2miDebugCounters.BbRejectedDflZero++;
            // #endregion
            return;
        }

        if ((header.Dfl & 7) != 0)
        {
            // #region agent log
            T2miDebugCounters.BbRejectedHeader++;
            // #endregion
            Logger.Send(LogStatus.ETSI,
                $"BBHEADER DFL ({header.Dfl}) is not byte-aligned (PLP {PlpId})");
            return;
        }

        if (header.Syncd != BbHeader.NoSyncInFrame)
        {
            if (header.Syncd >= header.Dfl)
            {
                Logger.Send(LogStatus.ETSI,
                    $"BBHEADER SYNCD ({header.Syncd}) >= DFL ({header.Dfl}) (PLP {PlpId})");
                return;
            }

            if ((header.Syncd & 7) != 0)
            {
                Logger.Send(LogStatus.ETSI,
                    $"BBHEADER SYNCD ({header.Syncd}) is not byte-aligned (PLP {PlpId})");
                return;
            }
        }

        if (mode == 0)
        {
            if (header.Upl == 0 || (header.Upl & 7) != 0)
            {
                Logger.Send(LogStatus.ETSI,
                    $"BBHEADER UPL ({header.Upl}) is invalid for normal mode (PLP {PlpId})");
                return;
            }

            if (header.Sync != 0 && (header.Sync != TsPacket.SYNC_BYTE || header.Syncd != 0))
            {
                Logger.Send(LogStatus.ETSI,
                    $"BBHEADER invalid SYNC/SYNCD combination ({header.Sync:X2}, {header.Syncd}) for normal mode (PLP {PlpId})");
                return;
            }
        }

        var frameData = data.Slice(BbHeader.Size);
        var cbDfl = header.Dfl >> 3;

        if (mode == 1)
        {
            ProcessHighEfficiency(header, frameData, cbDfl);
        }
        else
        {
            ProcessNormal(header, frameData, cbDfl);
        }

        _output.Flush(OnTsPacketsReady);
    }

    private void ProcessHighEfficiency(BbHeader header, ReadOnlySpan<byte> frameData, int cbDfl)
    {
        var cbNpd = header.Npd ? 1 : 0;
        var cbUpl = 187 + cbNpd;

        if (header.Syncd == BbHeader.NoSyncInFrame)
        {
            ProcessNoSyncdFrame(header, frameData, cbDfl, cbUpl, cbNpd, highEfficiency: true);
            return;
        }

        var cbSyncd = header.Syncd >> 3;
        if (_writtenSoFar != 0)
        {
            if (_writtenSoFar + cbSyncd != cbUpl)
            {
                Logger.Send(LogStatus.ETSI,
                    $"BBFRAME HE cache reset (cached {_writtenSoFar}, expected SYNCD {(cbUpl - _writtenSoFar) << 3}, got {header.Syncd}) (PLP {PlpId})");
                _writtenSoFar = 0;
            }
            else
            {
                Deliver(
                    NullPacketHeader.AsSpan(0, 1),
                    _partialBuffer.AsSpan(0, _writtenSoFar),
                    frameData.Slice(0, cbSyncd - cbNpd));
                InsertNullPackets(frameData, cbSyncd, cbNpd);
                _writtenSoFar = 0;
            }
        }

        frameData = frameData.Slice(cbSyncd);
        cbDfl -= cbSyncd;

        while (cbDfl >= cbUpl)
        {
            Deliver(
                NullPacketHeader.AsSpan(0, 1),
                frameData.Slice(0, cbUpl - cbNpd));
            InsertNullPackets(frameData, cbUpl, cbNpd);
            frameData = frameData.Slice(cbUpl);
            cbDfl -= cbUpl;
        }

        if (cbDfl > 0)
        {
            frameData.Slice(0, cbDfl).CopyTo(_partialBuffer.AsSpan(_writtenSoFar));
            _writtenSoFar += cbDfl;
        }
    }

    private void ProcessNormal(BbHeader header, ReadOnlySpan<byte> frameData, int cbDfl)
    {
        var cbUpl = header.Upl >> 3;
        var cbDnp = header.Npd ? 1 : 0;
        const int cbCrc8 = 1;
        var cbIssy = 0;
        var cbSync = header.Syncd != 0 ? 1 : 0;

        if (header.Issyi)
        {
            cbIssy = ((cbUpl + cbSync - cbDnp - cbCrc8) & 1) != 0 ? 3 : 2;
        }

        if (header.Syncd == BbHeader.NoSyncInFrame)
        {
            ProcessNoSyncdFrame(header, frameData, cbDfl, cbUpl, cbDnp, highEfficiency: false, cbIssy, cbCrc8);
            return;
        }

        if (header.Syncd == 0)
        {
            ProcessNormalSyncdZero(ref frameData, ref cbDfl, ref cbUpl, cbCrc8, cbIssy, cbDnp);
        }
        else
        {
            ProcessNormalSyncdNonZero(header, ref frameData, ref cbDfl, cbUpl, cbIssy, cbDnp);
        }

        while (cbDfl > cbUpl)
        {
            Deliver(frameData.Slice(cbCrc8, cbUpl - cbCrc8 - cbIssy - cbDnp));
            InsertNullPackets(frameData, cbUpl, cbDnp);
            frameData = frameData.Slice(cbUpl);
            cbDfl -= cbUpl;
        }

        if (cbDfl > 0)
        {
            frameData.Slice(0, cbDfl).CopyTo(_partialBuffer);
            _writtenSoFar = cbDfl;
            cbDfl = 0;
        }
    }

    private void ProcessNoSyncdFrame(
        BbHeader header,
        ReadOnlySpan<byte> frameData,
        int cbDfl,
        int cbUpl,
        int cbDnp,
        bool highEfficiency,
        int cbIssy = 0,
        int cbCrc8 = 0)
    {
        if (_writtenSoFar == 0)
        {
            return;
        }

        if (_writtenSoFar + cbDfl < cbUpl)
        {
            frameData.Slice(0, cbDfl).CopyTo(_partialBuffer.AsSpan(_writtenSoFar));
            _writtenSoFar += cbDfl;
            return;
        }

        if (_writtenSoFar + cbDfl != cbUpl)
        {
            Logger.Send(LogStatus.ETSI,
                $"Invalid BBFRAME DFL: expected {((cbUpl - _writtenSoFar) << 3)}, got {header.Dfl}, cache {_writtenSoFar} (PLP {PlpId})");
            _writtenSoFar = 0;
            return;
        }

        if (highEfficiency)
        {
            Deliver(
                NullPacketHeader.AsSpan(0, 1),
                _partialBuffer.AsSpan(0, _writtenSoFar),
                frameData.Slice(0, cbDfl - cbDnp));
        }
        else
        {
            Deliver(
                _partialBuffer.AsSpan(0, _writtenSoFar),
                frameData.Slice(0, cbDfl - cbIssy - cbDnp));
        }

        InsertNullPackets(frameData, cbDfl, cbDnp);
        _writtenSoFar = 0;
    }

    private void ProcessNormalSyncdZero(
        ref ReadOnlySpan<byte> frameData,
        ref int cbDfl,
        ref int cbUpl,
        int cbCrc8,
        int cbIssy,
        int cbDnp)
    {
        if (cbDfl < cbUpl)
        {
            if (cbCrc8 != 0)
            {
                _partialBuffer[0] = frameData[0];
            }

            _partialBuffer[cbCrc8] = TsPacket.SYNC_BYTE;
            frameData.Slice(cbCrc8, cbDfl - cbCrc8).CopyTo(_partialBuffer.AsSpan(cbCrc8 + 1));
            _writtenSoFar = cbDfl + 1;
            frameData = frameData.Slice(cbDfl);
            cbDfl = 0;
            return;
        }

        Deliver(
            NullPacketHeader.AsSpan(0, 1),
            frameData.Slice(cbCrc8, cbUpl - cbCrc8 - cbIssy - cbDnp));
        InsertNullPackets(frameData, cbUpl, cbDnp);
        _writtenSoFar = 0;
        frameData = frameData.Slice(cbUpl);
        cbDfl -= cbUpl;
        cbUpl += 1;
    }

    private void ProcessNormalSyncdNonZero(
        BbHeader header,
        ref ReadOnlySpan<byte> frameData,
        ref int cbDfl,
        int cbUpl,
        int cbIssy,
        int cbDnp)
    {
        var cbSyncd = header.Sync >> 3;
        if (_writtenSoFar != 0)
        {
            if (_writtenSoFar + cbSyncd != cbUpl)
            {
                Logger.Send(LogStatus.ETSI,
                    $"Invalid BBFRAME SYNCD: expected {(cbUpl - _writtenSoFar) << 3}, got {header.Syncd}, cache {_writtenSoFar} (PLP {PlpId})");
            }
            else
            {
                Deliver(
                    _partialBuffer.AsSpan(0, _writtenSoFar),
                    frameData.Slice(0, cbSyncd - cbIssy - cbDnp));
                InsertNullPackets(frameData, cbUpl, cbDnp);
            }

            _writtenSoFar = 0;
        }

        frameData = frameData.Slice(cbSyncd);
        cbDfl -= cbSyncd;
    }

    private void InsertNullPackets(ReadOnlySpan<byte> frameData, int packetBoundary, int cbDnp)
    {
        if (cbDnp == 0)
        {
            return;
        }

        var nullPacketsToAdd = frameData[packetBoundary - 1];
        var nullTail = NullPacketHeader.AsSpan();
        var padding = _partialBuffer.AsSpan(0, T2miAccessors.TsPacketSize - 4);
        padding.Clear();

        for (var i = 0; i < nullPacketsToAdd; i++)
        {
            Deliver(nullTail, padding);
        }
    }

    private void Deliver(ReadOnlySpan<byte> chunk0)
    {
        _output.Append(chunk0);
    }

    private void Deliver(ReadOnlySpan<byte> chunk0, ReadOnlySpan<byte> chunk1)
    {
        _output.Append(chunk0);
        _output.Append(chunk1);
    }

    private void Deliver(ReadOnlySpan<byte> chunk0, ReadOnlySpan<byte> chunk1, ReadOnlySpan<byte> chunk2)
    {
        _output.Append(chunk0);
        _output.Append(chunk1);
        _output.Append(chunk2);
    }

    private void OnTsPacketsReady(ReadOnlyMemory<byte> tsData) => TsPacketsReady?.Invoke(tsData);
}
