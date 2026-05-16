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
using TSParser.Descriptors;

namespace TSParser.Benchmarks.Descriptors;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ParseDescriptorBenchmarks
{
    private byte[] _bytes = null!;
    private byte? _callerTableId;

    [ParamsSource(nameof(DescriptorGroups))]
    public string DescriptorGroup { get; set; } = string.Empty;

    public static IEnumerable<string> DescriptorGroups() =>
        BenchmarkManifest.TopDescriptorGroups(10).Select(e => e.Group);

    [GlobalSetup]
    public void Setup()
    {
        var entry = BenchmarkManifest.TopDescriptorGroups(10)
            .First(e => e.Group.Equals(DescriptorGroup, StringComparison.OrdinalIgnoreCase));

        _bytes = BenchmarkResources.LoadBytes(entry.RelativePath);
        _callerTableId = BenchmarkResources.ParseCallerTableId(entry.CallerTableId);
    }

    [Benchmark]
    public Descriptor Parse() => TsParser.GetOneDescriptorFromBytes(_bytes, _callerTableId);
}
