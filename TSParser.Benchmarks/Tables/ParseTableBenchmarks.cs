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
using TSParser.Tables;

namespace TSParser.Benchmarks.Tables;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ParseTableBenchmarks
{
    private byte[] _sectionBytes = null!;
    private bool _useMipLoader;

    [ParamsSource(nameof(TableTypes))]
    public string TableType { get; set; } = string.Empty;

    public static IEnumerable<string> TableTypes() =>
        BenchmarkManifest.EnumerateTableSamples("L").Select(e => e.Type).Distinct(StringComparer.OrdinalIgnoreCase);

    [GlobalSetup]
    public void Setup()
    {
        var entry = BenchmarkManifest.EnumerateTableSamples("L")
            .First(e => e.Type.Equals(TableType, StringComparison.OrdinalIgnoreCase));

        _sectionBytes = BenchmarkResources.LoadBytes(entry.RelativePath);
        _useMipLoader = BenchmarkResources.UsesMipLoader(entry.Type);
    }

    [Benchmark]
    public Table Parse() => TsParser.GetOneTableFromBytes(_sectionBytes, _useMipLoader);
}
