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

using System;
using System.Collections.Generic;
using System.Net;
using TSParser.Analysis;
using TSParser.Enums;

namespace TSParser;

/// <summary>Immutable parser configuration for new code.</summary>
public sealed record ParserOptions
{
    public static ParserOptions Default { get; } = new();

    /// <summary>Allow analyzer packets to be processed.</summary>
    public bool AllowAnalyzer { get; init; }

    /// <summary>
    /// Bitrate measurement settings. When set and <see cref="BitrateMeasurementOptions.Enabled"/> is
    /// <see langword="true"/>, the analyzer is active regardless of <see cref="AllowAnalyzer"/>.
    /// </summary>
    public BitrateMeasurementOptions? BitrateMeasurement { get; init; }

    /// <summary>Maximum parser run time. Minimum value is 100 ms when set.</summary>
    public TimeSpan? ParserRunTime { get; init; }

    /// <summary>Current TS mode. Only DVB is functional.</summary>
    public TsMode CurrentTsMode { get; init; } = TsMode.DVB;

    /// <summary>Current decoder mode. Table or packet.</summary>
    public DecodeMode CurrentDecodeMode { get; init; } = DecodeMode.Packet;

    /// <summary>Optional TS file source.</summary>
    public string? TsFileName { get; init; }

    /// <summary>Optional UDP multicast source.</summary>
    public UdpSourceOptions? UdpSource { get; init; }

    /// <summary>T2-MI parsing options.</summary>
    public T2miOptions T2mi { get; init; } = T2miOptions.Disabled;

    internal static ParserOptions FromParserConfig(ParserConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var udpSource = config.MulticastGroup != null && config.MulticastPort != null
            ? new UdpSourceOptions(config.MulticastGroup)
            {
                MulticastPort = config.MulticastPort,
                IncomingIp = config.MulticastIncomingIp,
            }
            : null;

        return new ParserOptions
        {
            AllowAnalyzer = config.AllowAnalyzer,
            BitrateMeasurement = config.BitrateMeasurement,
            ParserRunTime = config.ParserRunTime.HasValue
                ? TimeSpan.FromMilliseconds(config.ParserRunTime.Value)
                : null,
            CurrentTsMode = config.CurrentTsMode,
            CurrentDecodeMode = config.CurrentDecodeMode,
            TsFileName = config.TsFileName,
            UdpSource = udpSource,
            T2mi = new T2miOptions
            {
                Enabled = config.T2miEnabled,
                Pids = config.T2miPids?.ToArray() ?? Array.Empty<ushort>(),
                AutoDetect = config.T2miAutoDetect,
                Deencapsulate = config.T2miDeencapsulate,
            },
        };
    }
}

/// <summary>UDP multicast source options.</summary>
public sealed record UdpSourceOptions
{
    public UdpSourceOptions(string multicastGroup)
    {
        MulticastGroup = multicastGroup;
    }

    /// <summary>Multicast group address.</summary>
    public string MulticastGroup { get; init; }

    /// <summary>Multicast destination port. Defaults to 1234 when omitted.</summary>
    public int? MulticastPort { get; init; }

    /// <summary>Incoming network interface address. Defaults to <see cref="IPAddress.Any"/> when omitted.</summary>
    public string? IncomingIp { get; init; }
}

/// <summary>T2-MI parsing options.</summary>
public sealed record T2miOptions
{
    public static T2miOptions Disabled { get; } = new();

    /// <summary>When true, reassemble and parse T2-MI on configured or auto-detected PIDs.</summary>
    public bool Enabled { get; init; }

    /// <summary>Explicit T2-MI PID list.</summary>
    public IReadOnlyList<ushort> Pids { get; init; } = Array.Empty<ushort>();

    /// <summary>After PAT/PMT, register a PID when the auto-detect heuristic matches.</summary>
    public bool AutoDetect { get; init; }

    /// <summary>When true, de-encapsulate baseband frames to MPEG-TS per PLP.</summary>
    public bool Deencapsulate { get; init; }
}
