# CPU profile: `ParseFullTs_AllTables` on `9.ts`

Captured **2026-05-22** on Windows x64, .NET 10, `Release`, `DOTNET_TC_QuickJitForLoop=0`.

| Item | Value |
|------|--------|
| TS file | `D:\Dvb\dvb_lib\9.ts` (~471 MB) |
| Workload | Same as `ParseFullTsBenchmarks.ParseFullTs_AllTables` (table mode, `AllowAnalyzer=true`) |
| Harness | `TSParser.Benchmarks.exe --profile-once` (single `RunParser()`, no BenchmarkDotNet) |
| Collector | `dotnet trace collect --profile cpu-sampling` |
| Artifacts | `ParseFullTs_AllTables_9ts.nettrace`, `*_top25_*.txt`, `*_top100_*.txt` |

Reproduce:

```powershell
$env:DOTNET_TC_QuickJitForLoop = '0'
$env:TSPARSER_PERF_TS_MEDIUM = 'D:\Dvb\dvb_lib\9.ts'
.\tools\run-trace-parsefullts.ps1
```

## Run notes

- **Not a hang:** a full BDN run (`warmupCount=1`, `iterationCount=3`) on `9.ts` is ~**40+ minutes** (~617 s per iteration in `perf/baselines/current.json`). Profiling that entire BDN session is slow and the first attempt produced a truncated `.nettrace`.
- **`--profile-once`** finishes in **~10–50 s** when the file is warm in the OS cache (PAT≥1 verified); use it for CPU samples of the parser hot path. For wall-clock regression, keep using BDN.
- **Async sampling bias:** `RunParser()` uses `Task.Run` for `FileTsSource.Run`; exclusive top frames include `Monitor.Wait` / `GetQueuedCompletionStatus` (~25% each) — I/O wait while the file pump runs on a thread-pool thread. Interpret parser cost from **inclusive** `TSParser.*` and `Buffer.Memmove` below.

## Top frames (exclusive, runtime)

| Rank | Symbol | Exclusive | Inclusive | Notes |
|------|--------|-----------|-----------|--------|
| 1–3 | `Monitor.Wait`, `GetQueuedCompletionStatus`, `WaitHandle.WaitOne` | ~24.7% each | — | Thread-pool / file-read wait (not parser logic) |
| 4 | `Buffer.MemmoveInternal` | **10.15%** | — | Copies (payload/section buffers; aligns with plan A1/A6) |
| 5 | `Thread.PollGC` | 8.79% | — | GC sampling |
| 6 | `OSFileStreamStrategy.Read` | 1.84% | 3.02% | File input |
| 9 | `FileTsSource.Run` | 0.27% | **24.59%** | Per-chunk read loop (plan A5: `GetPacketLength` per chunk lives here) |
| 10 | `TsParser.ParseBytesToTables` | 0.23% | **11.23%** | Per-packet table path entry |
| 16 | `DvbTableRouter.RouteDvb` | 0.08% | **5.63%** | Route every packet |
| 24 | `EIT.GetEvents` | 0.05% | 1.99% | EIT parse spike |

## Top frames (inclusive, `TSParser.*`)

| Inclusive % | Symbol | Plan tier |
|-------------|--------|-----------|
| 24.59% | `FileTsSource.Run` | A5 |
| 11.23% | `TsParser.ParseBytesToTables` | hot loop |
| 5.63% | `DvbTableRouter.RouteDvb` | A2 (most PIDs hit default branch) |
| 3.82% | `TsPacketFactory.GetTsPacket` | A1 (ctor + payload copy) |
| 2.92% | `DvbTableRouter.RouteTablePacket` | SI routing |
| 2.72% | `TableFactory.TryParseAssembledTable` | section complete |
| 2.17% | `SectionTableFactory.PushTable` | A6 assembly |
| 2.10% | `EitFactory.ParseTable` | SI spike |
| 1.98% | `DescriptorFactory.GetDescList` / `GetDescriptorList` | descriptor loops |
| 0.76% | `DescriptorFactory.GetDescriptor` | |
| 0.37% | `TsPacketFactory.GetTsPackets` | per-chunk array |
| 0.11% | `Analyzer.PushPacket` / `AddPacketToPidMetric` | analyzer (B3) |

`TsPacket` construction is not a separate leaf (likely inlined into `GetTsPacket`); inclusive **3.82%** on `GetTsPacket` is the best single marker for per-packet alloc/copy work.

`PsiSectionAssembler` / `AppendPayload` are &lt;0.1% exclusive on this warm short run; they matter more on cold runs with heavy EIT (`EitFactory.ParseTable` **2.1%** inclusive).

## Conclusion vs perf plan

Static hot-path prediction is **confirmed** for this trace:

1. Per-packet path (`ParseBytesToTables` → `GetTsPackets` / `GetTsPacket` → `RouteDvb`) dominates over table/ descriptor parse.
2. **`Buffer.Memmove`** is the largest parser-related exclusive bucket (copies).
3. **File read + async wait** dominate wall time; optimizing `FileTsSource` (A5) and avoiding full `TsPacket` on non-SI PIDs (A1/A2) remain the highest ROI.
4. **EIT + descriptor factories** show clear inclusive spikes but are secondary to the per-packet loop on `9.ts`.

After optimizations, re-run `.\tools\run-trace-parsefullts.ps1` and compare inclusive `ParseBytesToTables` / `GetTsPacket` / `Memmove`.
