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

using TSParser.Enums;
using TSParser.Tables.DvbTables;
using TSParser.TransportStream.T2mi;

namespace TSParser.Analysis;

/// <summary>
/// Lazily creates one <see cref="TsParser"/> per (T2-MI PID, PLP ID) and feeds copied inner TS from
/// <see cref="OnPlpTsReady"/> into table mode for PAT/SDT/PMT aggregation.
/// </summary>
public sealed class PlpInnerParserHost : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<(ushort T2miPid, byte PlpId), TsParser> _parsers = new();
    private bool _disposed;

    public PlpInnerParserHost(PlpServiceAggregator? aggregator = null)
    {
        Aggregator = aggregator ?? new PlpServiceAggregator();
    }

    public PlpServiceAggregator Aggregator { get; }

    /// <summary>
    /// Handles outer <see cref="TsParser.OnPlpTsReady"/> — copies <paramref name="tsData"/> then
    /// <see cref="TsParser.PushBytes"/> on the inner parser for this PLP.
    /// </summary>
    public void OnPlpTsReady(ushort t2miSourcePid, byte plpId, ReadOnlyMemory<byte> tsData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (tsData.IsEmpty)
            return;

        Aggregator.MarkPlpTsReceived(t2miSourcePid, plpId);

        var parser = GetOrCreateParser(t2miSourcePid, plpId);
        var copy = tsData.ToArray();
        parser.PushBytes(copy, T2miAccessors.TsPacketSize);
    }

    private TsParser GetOrCreateParser(ushort t2miPid, byte plpId)
    {
        lock (_sync)
        {
            var key = (t2miPid, plpId);
            if (_parsers.TryGetValue(key, out var existing))
                return existing;

            var parser = new TsParser(new ParserConfig
            {
                CurrentDecodeMode = DecodeMode.Table,
            });

            parser.OnPatReady += pat => Aggregator.ApplyPat(t2miPid, plpId, pat);
            parser.OnSdtReady += sdt => Aggregator.ApplySdt(t2miPid, plpId, sdt);
            parser.OnPmtReady += pmt => Aggregator.ApplyPmt(t2miPid, plpId, pmt);

            _parsers[key] = parser;
            return parser;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_sync)
        {
            foreach (var parser in _parsers.Values)
                parser.Dispose();

            _parsers.Clear();
        }

        _disposed = true;
    }
}
