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

namespace TSParser.TransportStream.T2mi;

/// <summary>DVB-T2 baseband frame header (10 bytes, ETSI EN 302 755).</summary>
public readonly struct BbHeader
{
    public const int Size = 10;
    public const ushort NoSyncInFrame = 0xFFFF;

    private readonly ReadOnlyMemory<byte> _raw;

    public BbHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            throw new ArgumentException($"BBHEADER requires at least {Size} bytes.", nameof(data));
        }

        var copy = new byte[Size];
        data.Slice(0, Size).CopyTo(copy);
        _raw = copy;
    }

    public ReadOnlySpan<byte> Raw => _raw.Span;

    public byte TsGs => (byte)(Raw[0] >> 6);
    public bool SisMis => (Raw[0] & 0x20) != 0;
    public bool CcmAcm => (Raw[0] & 0x10) != 0;
    public bool Issyi => (Raw[0] & 0x08) != 0;
    public bool Npd => (Raw[0] & 0x04) != 0;
    public byte Ext => (byte)(Raw[0] & 0x03);
    public byte Isi => Raw[1];
    public ushort Upl => BinaryPrimitives.ReadUInt16BigEndian(Raw.Slice(2));
    public ushort Dfl => BinaryPrimitives.ReadUInt16BigEndian(Raw.Slice(4));
    public byte Sync => Raw[6];
    public ushort Syncd => BinaryPrimitives.ReadUInt16BigEndian(Raw.Slice(7));
    public byte Mode => Raw[9];
}
