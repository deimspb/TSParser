# TSParser performance benchmarks

Reference timings for SI table/descriptor parsing and optional full transport-stream file runs. Results are produced with [BenchmarkDotNet](https://benchmarkdotnet.org/) in `TSParser.Benchmarks` and stored under `perf/baselines/`.

## Environment pinning

Use the **same machine**, power profile, and .NET SDK when capturing or comparing baselines.

| Setting | Recommendation |
|---------|----------------|
| Configuration | `Release`, `x64` (`Platform=x64`) |
| Power | High performance / plugged in; disable CPU throttling where possible |
| .NET | Document `dotnet --version` in the baseline file (written automatically) |
| JIT | `$env:DOTNET_TC_QuickJitForLoop = '0'` for more stable loop timings |
| Antivirus | Exclude repo `bin/` and `BenchmarkDotNet.Artifacts/` during runs |

Record CPU model and clock in commit messages when refreshing baselines. The baseline JSON includes `cpu`, `machine`, `runtime`, and `rid`.

## Fixtures and TS corpus

| Variable | Purpose |
|----------|---------|
| `TSPARSER_TEST_CORPUS` | Override `TestResources` (table/descriptor `.tbl` / `.desc` fixtures) |
| `TSPARSER_PERF_TS_MEDIUM` | Medium `.ts` for `ParseFullTsBenchmarks` |
| `TSPARSER_PERF_TS_LARGE` | Large `.ts` for `ParseFullTsLargeBenchmarks` |
| `TSPARSER_TS_ROOT` / `TSPARSER_PERF_TS` | Corpus directory (default `D:\Dvb\dvb_lib`) |

Fixture benchmarks do not require a `.ts` file. Full-file benchmarks are in category `RequiresTsFile` and are skipped unless you pass `-IncludeFullTs` to `run-benchmarks.ps1`.

## Commands

From the repository root:

```powershell
cd tools

# Fixture benchmarks only (tables, descriptors, loops)
$env:DOTNET_TC_QuickJitForLoop = '0'
.\run-benchmarks.ps1

# Include full .ts file parsing (needs corpus)
.\run-benchmarks.ps1 -IncludeFullTs

# Refresh committed baseline after an intentional change
.\run-benchmarks.ps1
.\update-perf-baseline.ps1

# Compare latest run to perf/baselines/net10.0-windows-x64.json
.\run-benchmarks.ps1
.\compare-perf.ps1
```

Direct `dotnet` invocation:

```powershell
dotnet run -c Release --project TSParser.Benchmarks -p:Platform=x64 -- `
  --filter *ParseTable* *ParseDescriptor* *DescriptorLoop*
```

Each run writes `perf/baselines/current.json` (via `PerfBaselineExporter`). Markdown summaries are under `BenchmarkDotNet.Artifacts/results/`.

## Baseline format

`perf/baselines/*.json` (schema version 1):

- `benchmarks.<key>.meanNs` / `stdDevNs` — wall-clock mean from BDN
- `benchmarks.<key>.allocatedBytes` — bytes allocated per invocation (when reported)
- Keys look like `ParseTableBenchmarks.Parse(TableType=PMT)`

## Regression thresholds

`tools/compare-perf.ps1`:

- **≥ 5%** slower mean → warning
- **≥ 10%** slower mean → exit code 1 (fail)

Allocated bytes are printed for information; comparison failure is based on mean time only.

## Benchmark suite

| Class | What is measured |
|-------|------------------|
| `ParseTableBenchmarks` | `TsParser.GetOneTableFromBytes` per SI type (L sample from manifest) |
| `ParseDescriptorBenchmarks` | `GetOneDescriptorFromBytes` for top 10 descriptor groups |
| `DescriptorLoopBenchmarks` | Full `PMT` parse vs synthetic ~4 KB descriptor loop |
| `ParseFullTsBenchmarks` | `TsParser` file mode, table vs packet decode (medium file) |
| `ParseFullTsLargeBenchmarks` | File mode, table decode (largest file in corpus) |
