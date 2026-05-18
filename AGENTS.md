# AGENTS.md — TSParser

Operational context for AI-assisted development. End-user quick start: [Readme.md](Readme.md).

**API audit** (2026-05-18): sections below match `TsParser.cs`, `ParserConfig`, `DescriptorFactory.cs`, T2-MI types under `TransportStream/T2mi/`, and `dotnet build TSParser.sln` on this tree.

---

## 1. Repository map

```
TSParser/                 # Core library (ship target), net10.0
  TransportStream/        # TsPacket, AdaptationField, PesHeader, TsPacketFactory
    T2mi/                 # T2-MI reassembly + BBFRAME → MPEG-TS (not PSI; separate from MIP)
  Tables/
    Table.cs              # abstract record — section header + CRC
    TableFactory.cs       # PSI/SI section reassembly across TS packets
    DvbTables/            # PAT, PMT, NIT, SDT, EIT, BAT, CAT, TDT, TOT, AIT, EWS, EEWS, SCTE35
    DvbTableFactory/      # PatFactory, PmtFactory, … — wired from TsParser
    Scte35/, Mip/
  Descriptors/
    Descriptor.cs
    DescriptorFactory.cs  # tag dispatch; unknown-once logging
    Dvb/, ExtendedDvb/, AitDescriptors/, Scte35Descriptors/
    Custom/               # operator descriptors (present in repo; see §6)
  Analysis/               # Analyzer, BitrateWindowMeasurer; PLP inner TS: PlpInnerParserHost, PlpServiceAggregator, PlpServiceReportFormatter
  Service/                # Logger, SectionParseValidation, SpliceInfoSectionType (XML schema types)
  Convertors/             # Scte35ToXml
  Buffers/, Comparer/, DictionariesData/, Enums/
TSParser.Tests/           # NUnit + manifest JSON + TestResources/**/*.tbl|.desc
TSParser.Benchmarks/      # BenchmarkDotNet
tools/CorpusHarvester/    # harvest real TS → descriptor fixtures
tools/BlessManifest/      # bless/refresh manifest.descriptors.json
StreamParser/             # Local sample CLI (.gitignore); T2-MI PLP service listing via --plp_services
```

---

## 2. Architecture and conventions (code changes)

### Data flow

bytes → `TsPacketFactory.GetTsPackets` → (`DecodeMode`) → per-PID `TableFactory.AddData` until section complete → parse → event (table mode), or `OnTsPacketReady` (packet mode). When `ParserConfig.T2miEnabled`, matching PIDs also go to `T2miDemuxer` → `OnT2miPacketReady` / optional `OnPlpTsReady(t2miSourcePid, plpId, …)` (not fed back into `TsParser` automatically). Nested inner-TS parsing is the consumer’s responsibility (see §3.8).

### Adding features

| Change | Where |
|--------|--------|
| New SI table | Record in `DvbTables/`, factory in `DvbTableFactory/`, wire in `TsParser` (`DvbTableFactory`), public event + delegate, `manifest.tables.json` entry, `.tbl` under `TestResources`, smoke test |
| New descriptor | `*Descriptor_0xNN.cs`; case in `DescriptorFactory` (`GetCustomDescriptor` for operator tags); `0x7F` → `GetExtensionDescriptor`; AIT (`table_id` `0x74`) / SCTE-35 (`0xFC`) use `GetAitDescriptor` / `GetSpliceDescriptor` via `GetDescriptorList` |

### Naming and parsing

- **Descriptors:** `{Name}Descriptor_0x{tag hex}` (e.g. `ServiceDescriptor_0x48`).
- **Tables:** `{NAME}.cs` in `DvbTables/` (e.g. `PAT`, `PMT`).
- **Spans:** parse from `ReadOnlySpan<byte>`; validate with `SectionParseValidation` / throw `SectionParseException`.
- **Descriptor loops:** do not swallow parse errors; rethrow `SectionParseException`, log other exceptions via `Logger.Send` and fall back only where the factory already does.

### Logging

- Use `Logger.Send(LogStatus, …)`; consumers subscribe to `Logger.OnLogMessage`.
- Unknown descriptor / extension / AIT / splice tags: log once per tag (`ConcurrentDictionary` in `DescriptorFactory`).

### Tests

- Resolve fixtures via [FixtureLoader](TSParser.Tests/Helpers/FixtureLoader.cs) and enumerate via [ManifestReader](TSParser.Tests/Helpers/ManifestReader.cs) — do not hardcode absolute paths in tests.
- After adding or changing `.tbl` / `.desc` files, run **BlessManifest** so CRC/hash and expected fields in manifest JSON stay in sync.

---

## 3. Public API (verified)

### 3.1 `ParserConfig` (class, public fields)

| Member | Type | Default | Notes |
|--------|------|---------|--------|
| `AllowAnalyzer` | `bool` | `false` | CC / legacy per-PID rate via `OnRate` |
| `BitrateMeasurement` | `BitrateMeasurementOptions?` | `null` | When `Enabled == true`, analyzer runs even if `AllowAnalyzer == false` |
| `ParserRunTime` | `int?` | `null` | Max run time (ms); minimum **100** when set |
| `CurrentTsMode` | `TsMode` | `DVB` | Only `DVB` is functional; `ATSC` / `ISDB` select factories that throw |
| `CurrentDecodeMode` | `DecodeMode` | `Packet` | `Packet` → `OnTsPacketReady`; `Table` → SI table events |
| `TsFileName` | `string?` | `null` | File mode; file must exist and be **≥ 2040** bytes |
| `MulticastGroup` | `string?` | `null` | UDP mode |
| `MulticastPort` | `int?` | `null` | UDP mode (default **1234** if null in UDP path) |
| `MulticastIncomingIp` | `string?` | `null` | Bind address; `IPAddress.Any` if null |
| `T2miEnabled` | `bool` | `false` | Reassemble T2-MI on `T2miPids` and/or auto-detected PID |
| `T2miPids` | `ushort[]?` | `null` | Explicit T2-MI PIDs (e.g. `0x1000`); registered at ctor when non-empty |
| `T2miAutoDetect` | `bool` | `false` | After PMT: one PAT program, one ES, `stream_type == 0x06` → register ES PID |
| `T2miDeencapsulate` | `bool` | `false` | With `T2miEnabled`, run `BbFrameStripper` per PLP → `OnPlpTsReady` |

Constructor `TsParser(ParserConfig)` starts file or UDP parsing internally when `TsFileName` or `MulticastGroup`+`MulticastPort` is set. Otherwise it only configures delegates (no `RunParser` until called).

### 3.2 `TsParser` lifecycle

| API | Notes |
|-----|--------|
| `TsParser(ParserConfig)` | File / multicast / configured push |
| `TsParser()` | **Lab helpers only** (`GetOneTsPacketFromBytes`, etc.); does **not** set `ParserModeDel` / `SelectedTableFactory` — **`PushBytes` will null-ref** unless you use `ParserConfig` ctor |
| `RunParser()` / `RunParserAsync()` / `StopParser()` | |
| `PushBytes(byte[] bytes, int packetLength)` | 188 or 204; DekTec-style feed without `RunParser` when input is external |
| `Dispose()` | Cancels tasks, closes UDP socket |

**Instance members:** `PacketSize` (`188`, `204`), `PidList` (from analyzer), `EwsPidList` / `EewsPidList` (must be set for EWS/EEWS table delivery).

**Static helpers:**

| Method | Purpose |
|--------|---------|
| `GetOneTableFromBytes(bytes, mip: false)` | Single section bytes → `Table`; `mip: true` → `MIP` |
| `GetOneDescriptorFromBytes(bytes, callerTableId?)` | `0x74` → AIT; `0xFC` → SCTE-35 splice descriptors; else DVB |
| `GetOneTsPacketFromBytes` / `GetTsPacketsFromBytes` | Packet-level parsing |
| `CompareTables(t1, t2)` | Structural diff as `IEnumerable<string>` |
| `CreateT2miDemuxer(pid, deencapsulate?)` | Standalone `T2miDemuxer` for lab/tests; `PushPacket(TsPacket)` or `PushPacket(ReadOnlySpan<byte>, …)` |

`DescriptorFactory` is **internal** — consumers use `GetOneDescriptorFromBytes` or table object graphs.

**Public T2-MI types** (namespace `TSParser.TransportStream.T2mi`): `T2miDemuxer`, `T2miPacket`, `T2miPacketType`, `T2miPacketAssembler` (low-level reassembly). `BbFrameStripper` / `BbHeader` are public for tests; typical apps use `TsParser` events or `T2miDemuxer` only.

### 3.3 Events (exact names from `TsParser.cs`)

| Event | Delegate | Payload type |
|-------|----------|----------------|
| `OnParserComplete` | `ParserComplete` | — |
| `OnPatReady` | `PatReady` | `PAT` |
| `OnPmtReady` | `PmtReady` | `PMT` |
| `OnCatReady` | `CatReady` | `CAT` |
| `OnNitReady` | `NitReady` | `NIT` |
| `OnSdtReady` | `SdtReady` | `SDT` |
| `OnBatReady` | `BatReady` | `BAT` |
| `OnEitReady` | `EitReady` | `EIT` |
| `OnTdtReady` | `TdtReady` | `TDT` |
| `OnTotready` | `TotReady` | `TOT` | **Spelling is `OnTotready` (lowercase `r`) — public API typo** |
| `OnAitReady` | `AitReady` | `AIT` | PID discovered from PMT (stream type `0x05` + descriptor `0x6F`) |
| `OnMipReady` | `MipReady` | `MIP` | PID `0x15` (network sync) |
| `OnScte35Ready` | `Scte35Ready` | `SCTE35` | PID from PMT stream type `0x86` |
| `OnEwsReady` | `EwsReady` | `EWS` | Requires `EwsPidList` |
| `OnEewsReady` | `EewsReady` | `EEWS` | Requires `EewsPidList` |
| `OnTsPacketReady` | `TsPacketReady` | `TsPacket` | `DecodeMode.Packet` only |
| `OnRate` | `RateDelegate` | `ushort pid, ulong deltaPackets, ulong deltaTime` | Legacy analyzer |
| `OnBitrateMeasured` | `BitrateMeasuredDelegate` | `BitrateSample` | Needs `ParserConfig.BitrateMeasurement` |
| `OnT2miPacketReady` | `T2miPacketReady` | `T2miPacket` | Needs `T2miEnabled`; each reassembled T2-MI packet |
| `OnT2miPlpDiscovered` | `T2miPlpDiscovered` | `byte plpId` | First `PlpId` seen per demuxer (type `0x00` baseband) |
| `OnPlpTsReady` | `PlpTsReady` | `ushort t2miSourcePid`, `byte plpId`, `ReadOnlyMemory<byte> tsData` | Needs `T2miDeencapsulate`; 188-byte TS multiples; **`t2miSourcePid`** disambiguates PLP IDs across multiple T2-MI PIDs; **buffer valid only for callback** — copy before async work or `PushBytes` |

**Logging:** `Logger.OnLogMessage` (`TSParser.Service.Logger`).

**Not exposed as events** (logged at INFO only): RST, RNT, in-band signalling, measurement, DIT, SIT.

### 3.4 `GetOneTableFromBytes` table_id map

| `table_id` | Type |
|------------|------|
| `0x00` | `PAT` |
| `0x01` | `CAT` |
| `0x02` | `PMT` |
| `0x40`, `0x41` | `NIT` |
| `0x42`, `0x46` | `SDT` |
| `0x4A` | `BAT` |
| `0x4E`–`0x4F`, `0x50`–`0x5F`, `0x60`–`0x6F` | `EIT` |
| `0x70` | `TDT` |
| `0x73` | `TOT` |
| `0x74` | `AIT` |
| `0x93` | `EWS` |
| `0x94`, `0x95` | `EEWS` |
| `0xFC` | `SCTE35` |
| `mip: true` (not `table_id`) | `MIP` |

### 3.5 `BitrateMeasurementOptions` (public)

`Enabled`, `MeasurementWindow` (default 1s), `ClockSource` (`Pcr` / `Pts` / `Dts`), `ReferencePid`, `MeasureStreamBitrate`, `MeasurePerPidBitrate`, `IncludeNullPackets`.

### 3.6 T2-MI vs MIP

| | **MIP** | **T2-MI** |
|---|---------|-----------|
| Standard / role | DVB-T modulator interface (network sync) | DVB-T2 modulator interface (ETSI TS 102 773) |
| Typical PID | Fixed `0x15` (`ReservedPids.NetworkSync`) | Operator-defined (e.g. `0x1000`); set via `T2miPids` |
| Integration | `MipFactory` + `TableFactory` → `OnMipReady` → `MIP` table type | `T2miDemuxer` per PID; **not** a PSI section |
| Payload | SI-style sections | Reassembled T2-MI packets; type `0x00` carries **PLP_ID** + baseband |
| Inner MPEG-TS | N/A | Optional: `T2miDeencapsulate` → `OnPlpTsReady(t2miSourcePid, plpId, …)`; consumer runs a second `TsParser` (or `PlpInnerParserHost`) on copied data |

T2-MI runs in both `DecodeMode.Table` and `DecodeMode.Packet` (wired after table dispatch or `OnTsPacketReady`). It does **not** use `Decoder` / `DecoderMode`. Multi-program streams often need explicit `T2miPids` because `T2miAutoDetect` requires exactly one PAT program and one elementary stream with `stream_type` `0x06`.

`PlpId` in SI is only metadata today (`T2DeliverySystemDescriptor_0x04` extension `0x04`); runtime PLP discovery is via T2-MI events above.

### 3.7 Other public types (do not treat as ready product API)

- `Decoder` — stub; `RunDecoder` / `Scte35Decoder` empty; **not** used for T2-MI.
- `Scte35ToXml.Convert(SCTE35)` — XML serialization helper.

### 3.8 PLP inner MPEG-TS services (`TSParser.Analysis` + StreamParser)

**`OnPlpTsReady` contract:** `PlpTsReady(ushort t2miSourcePid, byte plpId, ReadOnlyMemory<byte> tsData)`. `t2miSourcePid` is the outer MPEG-TS PID where the `T2miDemuxer` was registered (`ParserConfig.T2miPids` or auto-detect). `T2miDemuxer` still raises `Action<byte, ReadOnlyMemory<byte>>` internally; `TsParser` adds the source PID when forwarding to `OnPlpTsReady`.

**Nested parsing pattern** (verified in [`T2miDeencapsulationTests`](TSParser.Tests/T2mi/T2miDeencapsulationTests.cs), [`PlpServiceAggregatorTests`](TSParser.Tests/T2mi/PlpServiceAggregatorTests.cs)):

1. Outer `TsParser`: `T2miEnabled` + `T2miDeencapsulate` + explicit `T2miPids`; outer `CurrentDecodeMode` usually `Packet` (outer SI not required).
2. `OnPlpTsReady`: copy `tsData` (`ToArray()` or equivalent), then `PushBytes` on an inner `TsParser` per `(t2miSourcePid, plpId)` in `DecodeMode.Table`.
3. Aggregate PAT / SDT / PMT on inner events; report after `OnParserComplete`.

| Type | Role |
|------|------|
| `PlpInnerParserHost` | Lazy `Dictionary<(ushort t2miPid, byte plpId), TsParser>`; `OnPlpTsReady` handler copies buffer and `PushBytes` |
| `PlpServiceAggregator` | Merges PAT (`program_number`, PMT PID), SDT (`ServiceDescriptor_0x48`), PMT (PCR PID, ES list); keyed by T2-MI PID + PLP + program |
| `PlpServiceReportFormatter` | Human-readable stdout grouped T2-MI PID → PLP → services |
| `PlpServiceInfo` / `PlpElementaryStreamInfo` | Aggregated service / ES records |

Service names: SDT `0x48` first; fallback `ServiceDescriptor_0x48` in PMT program info. Partial output if PAT arrives without SDT.

**StreamParser** (local, not in CI): `--plp_services` with required `--t2mi_pids` (comma-separated hex `0x1000` or decimal `4096`). Ignores `-d` / decode verb selection. Processes **all** PLPs on each listed T2-MI PID (no PLP filter). Same outer config as above; wires `PlpInnerParserHost` and prints via `PlpServiceReportFormatter` on `OnParserComplete`. Exit code **1** if no inner TS was received (stderr warning).

```text
StreamParser.exe file -f <path.ts> --t2mi_pids 0x1000,4096 --plp_services [--run_time <ms>]
StreamParser.exe stream -m <group> -p <port> --t2mi_pids 0x1000 --plp_services [--run_time <ms>]
```

Does **not** use `T2miAutoDetect` alone — user supplies T2-MI PID(s) explicitly.

---

## 4. Supported tables (runtime vs fixtures)

**Stream parsing (DVB, `DecodeMode.Table`):** PAT, CAT, PMT, NIT, SDT, BAT, EIT, TDT, TOT, MIP, AIT (dynamic PID), SCTE-35 (dynamic PID), EWS, EEWS (operator PIDs).

**Manifest coverage** ([manifest.tables.json](TSParser.Tests/TestResources/manifest.tables.json)): `missing: true` for **EEWS**, **EWS**, **MIP**, **SCTE35** (types exist in code; corpus fixtures incomplete).

---

## 5. Descriptors (verified dispatch)

Resolution order in `GetDescriptor`: **`GetCustomDescriptor` first**, then DVB `switch`, then unknown → generic `Descriptor` (one log per tag).

### 5.1 Custom (`GetCustomDescriptor` — active)

| Tag | Type used at runtime |
|-----|----------------------|
| `0x09` | `CaDescriptorCustom_0x09` (**shadows** standard `CaDescriptor_0x09`) |
| `0x86` | `GnrDescriptor_0x86` |
| `0x87` | `LogicalChannelNumberDescriptorV2_0x87` |
| `0x88` | `MultilingualRegionNameDescriptor_0x88` |
| `0x89` | `EwsRegionDescriptor_0x89` |
| `0x90` | `EwsZoneDescriptor_0x90` |
| `0xB0` | `SettingsDescriptorV3_0xB0` |
| `0xB1` | `SettingsDescriptorV4_0xB1` |
| `0xB2` | `ChannelListTypeDescriptor_0xB2` |
| `0xB4` | `TimeZoneDescriptorLG_0xB4` |
| `0xC0` | `WhiteListDescriptor_0xC0` |

**Sources:** `TSParser/Descriptors/Custom/*.cs` (tracked in git; `dotnet build` succeeds).

**On disk but not wired in `GetCustomDescriptor`:** `SettingsDescriptorV1_0x89`, `SettingsDescriptorV2_0x90`, `TimeZoneDescriptor_0xB3` — tags `0x89`/`0x90` use EWS region/zone types instead; `0xB3` falls through to unknown/generic DVB handling.

### 5.2 DVB SI (`GetDescriptor` switch)

`0x02` `0x03` `0x05` `0x06` `0x09`* `0x0A` `0x0C` `0x0E` `0x0F` `0x11` `0x13` `0x14` `0x28` `0x2A` `0x38` `0x40` `0x41` `0x43` `0x44` `0x45` `0x47` `0x48` `0x4A` `0x4D` `0x4E` `0x50` `0x52` `0x53` `0x54` `0x55` `0x56` `0x58` `0x59` `0x5A` `0x5C` `0x5F` `0x60` `0x64` `0x66` `0x6A` `0x6C` `0x6D` `0x6F` `0x70` `0x7C` `0x7F` `0x83` `0x8A`

\*Standard `0x09` never reached when custom `0x09` matches first.

### 5.3 Extension (`0x7F`, byte at index 2)

`0x00` ImageIcon, `0x04` T2DeliverySystem; else `ExtendedDescriptor`.

### 5.4 AIT (`GetAitDescriptor`, `callerTableId == 0x74`)

`0x00`–`0x04`, `0x10`, `0x15`; else `AitDescriptor`.

### 5.5 SCTE-35 splice (`GetSpliceDescriptor`, `callerTableId == 0xFC`)

`0x00`–`0x04` Avail, DTMF, Segmentation, Time, Audio; else `Scte35Descriptor`.

Full class names: see switch in [DescriptorFactory.cs](TSParser/Descriptors/DescriptorFactory.cs).

---

## 6. README vs code (keep in sync)

End-user docs: [Readme.md](Readme.md) (Russian). When editing README, prefer this file for API truth.

| Topic | Code truth (do not regress in docs) |
|-------|-------------------------------------|
| Custom `0xB3` | `TimeZoneDescriptor_0xB3.cs` exists but **not** wired in `GetCustomDescriptor`; runtime uses `0xB4` (`TimeZoneDescriptorLG`) |
| Custom `0x89` / `0x90` | EWS region/zone types active; legacy `SettingsDescriptorV1/V2` sources exist but are commented out in factory |
| `OnTotready` | Public API typo — event name is `OnTotready`, not `OnTotReady` |
| `TsParser()` | Static/table helpers only; **`PushBytes` and stream parsing need `TsParser(ParserConfig)`** |
| `TsMode.ATSC` / `ISDB` | Enum values select factories that throw `NotImplementedException` on first SI packet |
| MIP vs T2-MI | `OnMipReady` / PID `0x15` is DVB-T MIP; T2-MI uses `T2mi*` config/events on arbitrary PIDs |
| `OnPlpTsReady` | Signature `ushort t2miSourcePid, byte plpId, ReadOnlyMemory<byte> tsData`; buffer valid only during callback — copy before `PushBytes` or async use |
| StreamParser `--plp_services` | Requires `--t2mi_pids`; uses `TSParser.Analysis` host/aggregator; not in minimal clone (`.gitignore`) |

---

## 7. Commands

```bash
dotnet build TSParser.sln
dotnet test TSParser.Tests/TSParser.Tests.csproj
dotnet run --project TSParser.Benchmarks -c Release
```

**StreamParser** (local checkout only; project may be gitignored):

```bash
dotnet run --project StreamParser -- file -f path.ts --t2mi_pids 0x1000 --plp_services --run_time 5000
dotnet run --project StreamParser -- stream -m 239.0.0.1 -p 1234 --t2mi_pids 4096 --plp_services
```

**Tools** (from repo root):

```bash
dotnet run --project tools/CorpusHarvester -- harvest
dotnet run --project tools/CorpusHarvester -- select
dotnet run --project tools/BlessManifest
```

On Windows, equivalent wrappers: `tools\harvest-tables.ps1`, `tools\select-samples.ps1`, `tools\bless-manifest.ps1`.

**Environment variables**

| Variable | Used by | Purpose |
|----------|---------|---------|
| `TSPARSER_TEST_CORPUS` | `FixtureLoader`, benchmarks | Override `TestResources` root when **running tests** |
| `TSPARSER_TEST_FIXTURES` | CorpusHarvester, BlessManifest, PS scripts | Alternate fixtures root for **harvest/bless** |
| `TSPARSER_TS_ROOT` | CorpusHarvester, harvest scripts | Directory of input `.ts` files |
| `TSPARSER_DESCRIPTOR_STAGING` | CorpusHarvester `harvest` / `select` | Staging for harvested `.desc` |
| `TSPARSER_TABLE_STAGING` | Table harvest/select scripts | Staging for harvested `.tbl` |
| `TSPARSER_T2MI_SAMPLE` | `FixtureLoader`, T2-MI integration tests | Override path to full `t2mi_cut.ts` (bundled sample: `TestResources/T2mi/t2mi_cut_pid1000.ts`) |

---

## 8. Manifest / fixture workflow

1. **Tables (optional corpus path):** harvest `.ts` → staging (`tools/harvest-tables.ps1`) → copy samples (`tools/select-samples.ps1`) into `TSParser.Tests/TestResources/Tables/`.
2. **Descriptors:** `CorpusHarvester harvest` (parse TS, unique `.desc` to staging) → `CorpusHarvester select` (copy S/M1/M2/L samples into `TestResources/Descriptors/`).
3. **Bless:** run BlessManifest (or `bless-manifest.ps1`) to parse every on-disk fixture and rewrite:
   - [manifest.tables.json](TSParser.Tests/TestResources/manifest.tables.json) — CRC, lengths, type-specific expected fields; `missing: true` marks types without corpus samples yet (**EEWS**, **EWS**, **MIP**, **SCTE35**).
   - [manifest.descriptors.json](TSParser.Tests/TestResources/manifest.descriptors.json) — grouped descriptor entries.
4. **Test:** manifest-driven tests load fixtures via `ManifestReader` / `FixtureLoader`; `dotnet test` must pass before merging descriptor/table changes.

BlessManifest flags: `--tables-only`, `--descriptors-only`, `--fixtures-root <path>`.

---

## 9. Known gaps

- `TsMode.ATSC` / `TsMode.ISDB` — enum only; `AtscTableFactory` / `IsdbTableFactory` throw.
- `TransportStream/NAL/` — placeholder folder in csproj.
- `Decoder` — not implemented (T2-MI uses dedicated `T2miDemuxer` pipeline instead).
- **T2-MI:** `T2miDescriptor_0x11` (extension tag `0x7F` / `0x11`) not wired in `DescriptorFactory`; auto-detect relies on PAT/PMT heuristic or `T2miPids` only.
- Solution references **AppTest** and **StreamParser** sample apps (`.gitignore` excludes them — absent in a minimal clone; `StreamParser` may warn on missing DekTec `DTAPINET` refs). When present, `StreamParser --plp_services` depends on `TSParser.Analysis` PLP types above.
- Custom tags `0xB3`, legacy `SettingsDescriptorV1/V2` — source files exist, factory wiring incomplete.

---

## 10. Standards

- [ETSI EN 300 468](https://www.etsi.org/) (DVB SI)
- [ISO/IEC 13818-1](https://www.iso.org/) (MPEG-2 TS)
- [ETSI TS 102 773](https://www.etsi.org/) (T2-MI)
- [ETSI EN 302 755](https://www.etsi.org/) (DVB-T2 baseband / BBHEADER)
- [SCTE-35](https://www.scte.org/) — XML schema: http://www.scte.org/schemas/35

---

## 11. License

Apache 2.0 — [TSParser/LICENCE](TSParser/LICENCE)
