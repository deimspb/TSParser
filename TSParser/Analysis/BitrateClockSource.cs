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

namespace TSParser.Analysis
{
    /// <summary>Clock used to pace bitrate measurement windows.</summary>
    public enum BitrateClockSource
    {
        /// <summary>
        /// Program Clock Reference (27 MHz). Recommended for multiplex transport bitrate (TR 101 290);
        /// markers are typically the most regular.
        /// </summary>
        Pcr,

        /// <summary>
        /// Presentation Time Stamp (90 kHz). Present on PES with PTS; suitable for per-PID measurement.
        /// For whole-TS windows, set <see cref="BitrateMeasurementOptions.ReferencePid"/> (for example video).
        /// </summary>
        Pts,

        /// <summary>
        /// Decoding Time Stamp (90 kHz). Present only when both PTS and DTS are carried in PES.
        /// Same whole-TS constraints as <see cref="Pts"/>.
        /// </summary>
        Dts,

        /// <summary>
        /// Synthetic clock from accumulated transport bytes at
        /// <see cref="BitrateMeasurementOptions.AssumedBitsPerSecond"/> (default 10 Mb/s).
        /// Use when PCR/PTS/DTS are absent so bitrate windows can still advance.
        /// </summary>
        AssumedTransportRate
    }
}
