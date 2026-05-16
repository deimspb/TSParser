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
using TSParser.Tables.DvbTables;

namespace TSParser.Benchmarks.Descriptors;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class DescriptorLoopBenchmarks
{
    private byte[] _pmtSection = null!;
    private byte[] _syntheticLoop = null!;

    [GlobalSetup]
    public void Setup()
    {
        var pmtBytes = BenchmarkResources.TryGetTableSampleBytes("PMT", "L")
            ?? throw new InvalidOperationException("PMT_L fixture is required for descriptor loop benchmarks.");

        _pmtSection = pmtBytes;
        _syntheticLoop = BenchmarkResources.BuildSyntheticDescriptorLoop(4096);
    }

    [Benchmark(Description = "PMT section (includes descriptor loops)")]
    public PMT ParsePmtSection() => new(_pmtSection);

    [Benchmark(Description = "Synthetic ~4KB loop via GetOneDescriptorFromBytes")]
    public int ParseSyntheticLoop()
    {
        var count = 0;
        var offset = 0;
        while (offset < _syntheticLoop.Length)
        {
            var span = _syntheticLoop.AsSpan(offset);
            if (span.Length < 2)
            {
                break;
            }

            var desc = TsParser.GetOneDescriptorFromBytes(span);
            offset += desc.DescriptorTotalLength;
            count++;
        }

        return count;
    }
}
