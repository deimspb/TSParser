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

namespace TSParser.TransportStream.T2mi;

/// <summary>Demultiplexes one MPEG-TS PID carrying T2-MI into reassembled <see cref="T2miPacket"/> instances.</summary>
public sealed class T2miDemuxer
{
    private readonly T2miPacketAssembler _assembler = new();
    private readonly HashSet<byte> _discoveredPlps = new();
    private readonly Dictionary<byte, BbFrameStripper> _bbStrippers = new();

    public T2miDemuxer(ushort pid, bool deencapsulate = false)
    {
        Pid = pid;
        Deencapsulate = deencapsulate;
        _assembler.PacketReady += OnAssemblerPacketReady;
    }

    public ushort Pid { get; }

    /// <summary>When true, type 0x00 baseband payloads are passed through <see cref="BbFrameStripper"/>.</summary>
    public bool Deencapsulate { get; set; }

    public event Action<T2miPacket>? PacketReady;

    public event Action<byte>? PlpDiscovered;

    /// <summary>Complete 188-byte TS packet(s). Buffer is valid only for the duration of the callback.</summary>
    public event Action<byte, ReadOnlyMemory<byte>>? PlpTsReady;

    public void Reset()
    {
        _assembler.Reset();
        _discoveredPlps.Clear();
        foreach (var stripper in _bbStrippers.Values)
        {
            stripper.Reset();
        }
    }

    public void PushPacket(TsPacket tsPacket)
    {
        if (tsPacket.TransportErrorIndicator || tsPacket.Pid != Pid)
        {
            return;
        }

        var raw = tsPacket.RawPacket;
        if (raw == null || raw.Length != T2miAccessors.TsPacketSize)
        {
            return;
        }

        PushPacket(raw, tsPacket.Pid, tsPacket.PacketNumber);
    }

    public void PushPacket(ReadOnlySpan<byte> tsPacket, ushort sourcePid = 0, ulong packetNumber = 0)
    {
        if (tsPacket.Length != T2miAccessors.TsPacketSize)
        {
            throw new ArgumentException($"Expected {T2miAccessors.TsPacketSize} bytes.", nameof(tsPacket));
        }

        _assembler.PushPacket(tsPacket, sourcePid != 0 ? sourcePid : Pid, packetNumber);
    }

    private void OnAssemblerPacketReady(T2miPacket packet)
    {
        if (packet.PlpId is byte plpId && _discoveredPlps.Add(plpId))
        {
            PlpDiscovered?.Invoke(plpId);
        }

        if (Deencapsulate
            && packet.PacketType == T2miPacketType.BasebandFrame
            && packet.Crc32Valid
            && packet.Payload.Length >= BbHeader.Size)
        {
            var stripper = GetOrCreateStripper(packet.PlpId ?? 0);
            stripper.Receive(packet.Payload);
        }

        PacketReady?.Invoke(packet);
    }

    private BbFrameStripper GetOrCreateStripper(byte plpId)
    {
        if (_bbStrippers.TryGetValue(plpId, out var stripper))
        {
            return stripper;
        }

        stripper = new BbFrameStripper(plpId);
        stripper.TsPacketsReady += data => PlpTsReady?.Invoke(plpId, data);
        _bbStrippers.Add(plpId, stripper);
        return stripper;
    }
}
