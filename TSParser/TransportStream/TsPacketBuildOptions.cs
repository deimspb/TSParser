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

namespace TSParser.TransportStream;

internal readonly struct TsPacketBuildOptions
{
    public bool CaptureRawPacket { get; init; }
    public bool IncludePayload { get; init; }
    public bool ParsePesHeader { get; init; }

    public static TsPacketBuildOptions FullWithRaw => new()
    {
        CaptureRawPacket = true,
        IncludePayload = true,
        ParsePesHeader = true,
    };

    public static TsPacketBuildOptions SiTable(bool captureRawPacket) => new()
    {
        CaptureRawPacket = captureRawPacket,
        IncludePayload = true,
        ParsePesHeader = false,
    };

    public static TsPacketBuildOptions HeaderOnly => new()
    {
        CaptureRawPacket = false,
        IncludePayload = false,
        ParsePesHeader = false,
    };
}
