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

using BenchmarkDotNet.Attributes;
using TSParser.Benchmarks.Infrastructure;
using TSParser.Enums;

namespace TSParser.Benchmarks.Transport;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
[BenchmarkCategory("RequiresTsFile")]
public class ParseFullTsBenchmarks
{
    private string _tsPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tsPath = BenchmarkResources.TryGetPerfTsMedium()
            ?? throw new InvalidOperationException(
                "Full TS benchmarks require a .ts file. Set TSPARSER_PERF_TS_MEDIUM, TSPARSER_PERF_TS, or TSPARSER_TS_ROOT (e.g. D:\\Dvb\\dvb_lib).");
    }

    [Benchmark(Description = "File parse, table decode mode")]
    public void ParseFullTs_AllTables()
    {
        var parser = new TsParser(new ParserConfig
        {
            TsFileName = _tsPath,
            CurrentDecodeMode = DecodeMode.Table,
            AllowAnalyzer = true,
        });

        parser.RunParser();
    }

    [Benchmark(Description = "File parse, packet decode mode")]
    public void ParseFullTs_PacketsOnly()
    {
        var parser = new TsParser(new ParserConfig
        {
            TsFileName = _tsPath,
            CurrentDecodeMode = DecodeMode.Packet,
            AllowAnalyzer = false,
        });

        parser.RunParser();
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
[BenchmarkCategory("RequiresTsFile")]
public class ParseFullTsLargeBenchmarks
{
    private string _tsPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tsPath = BenchmarkResources.TryGetPerfTsLarge()
            ?? throw new InvalidOperationException(
                "Large TS benchmark requires TSPARSER_PERF_TS_LARGE or a corpus directory with .ts files.");
    }

    [Benchmark(Description = "Large file parse, table decode mode")]
    public void ParseFullTs_Large()
    {
        var parser = new TsParser(new ParserConfig
        {
            TsFileName = _tsPath,
            CurrentDecodeMode = DecodeMode.Table,
            AllowAnalyzer = true,
        });

        parser.RunParser();
    }
}
