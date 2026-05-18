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

/// <summary>One reassembled T2-MI packet from a transport-stream PID.</summary>
public sealed class T2miPacket
{
    public T2miPacketType PacketType { get; init; }
    public byte SuperframeIndex { get; init; }
    public byte StreamId { get; init; }
    public byte? PlpId { get; init; }
    public byte? FrameIndex { get; init; }
    public bool? IntlFrameStart { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public bool Crc32Valid { get; init; }
    public ushort SourcePid { get; init; }
    public ulong PacketNumber { get; init; }
    public byte PacketCount { get; init; }
    public ushort Rfu { get; init; }
}
