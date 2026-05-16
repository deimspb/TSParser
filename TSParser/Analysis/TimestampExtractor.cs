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

namespace TSParser.Analysis
{
    /// <summary>Extracts PCR/PTS/DTS ticks from a transport packet for bitrate windows.</summary>
    internal static class TimestampExtractor
    {
        public static bool TryGetTimestamp(TsPacket packet, BitrateClockSource source, out ulong tick)
        {
            tick = 0;

            switch (source)
            {
                case BitrateClockSource.Pcr:
                    if (packet.HasAdaptationField && packet.Adaptation_field.PCRFlag)
                    {
                        tick = packet.Adaptation_field.PcrValue;
                        return true;
                    }
                    return false;

                case BitrateClockSource.Pts:
                    if (!packet.HasPesHeader)
                        return false;

                    var ptsFlags = packet.Pes_header.PTSDTSFlags;
                    if (ptsFlags != 0b10 && ptsFlags != 0b11)
                        return false;

                    tick = packet.Pes_header.PTSHex;
                    return true;

                case BitrateClockSource.Dts:
                    if (!packet.HasPesHeader || packet.Pes_header.PTSDTSFlags != 0b11)
                        return false;

                    tick = packet.Pes_header.DTSHex;
                    return true;

                default:
                    return false;
            }
        }
    }
}
