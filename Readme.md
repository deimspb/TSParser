# TSParser — библиотека разбора DVB/MPEG-2 Transport Stream

Библиотека на **.NET 10** для разбора MPEG-2 Transport Stream: TS-пакеты (188/204 байт), adaptation field, PES-заголовки, PSI/SI-таблицы DVB, SCTE-35, операторские дескрипторы и опциональный анализ CC/битрейта.

Лицензия: [Apache 2.0](TSParser/LICENCE).

---

## Возможности

- Разбор TS-пакетов, adaptation field и PES-заголовков; поддержка секций с ненулевым `pointer_field`.
- SI/PSI-таблицы DVB (см. [Поддерживаемые таблицы](#поддерживаемые-таблицы)).
- **SCTE-35** (PID из PMT, stream type `0x86`); сериализация в XML — [`Scte35ToXml`](TSParser/Convertors/Scte35ToXml.cs) ([схема SCTE-35](http://www.scte.org/schemas/35)).
- **AIT** (PID из PMT: stream type `0x05` + descriptor `0x6F`).
- **EWS / EEWS** — при заданных `EwsPidList` / `EewsPidList` на экземпляре `TsParser`.
- Дескрипторы: DVB, extension (`0x7F`), AIT, SCTE-35 splice, custom (операторские) — см. [Дескрипторы](#дескрипторы).
- Режимы ввода: файл, UDP multicast, внешняя подача байт (`PushBytes`, например DekTec).
- Анализатор: CC по PID, legacy-оценка скорости (`OnRate`), окно битрейта (`OnBitrateMeasured` + `BitrateMeasurementOptions`).

---

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

Сборка:

```bash
dotnet build TSParser.sln
```

---

## Быстрый старт

Подключите пакет/проект `TSParser`, namespace `TSParser`.

### Файл или multicast

Конструктор `TsParser(ParserConfig)` при указании `TsFileName` или пары `MulticastGroup` + `MulticastPort` сам запускает разбор после подписки на события и вызова `RunParser()` / `RunParserAsync()`.

```csharp
using TSParser;
using TSParser.Analysis;
using TSParser.Service;

var config = new ParserConfig
{
    TsFileName = @"C:\captures\stream.ts",
    CurrentDecodeMode = DecodeMode.Table,
    ParserRunTime = 60_000, // необязательно; минимум 100 мс, если задано
    BitrateMeasurement = new BitrateMeasurementOptions
    {
        Enabled = true,
        MeasurementWindow = TimeSpan.FromSeconds(1),
        MeasureStreamBitrate = true,
    },
};

using var parser = new TsParser(config);

parser.OnPatReady += pat => { /* PAT */ };
parser.OnPmtReady += pmt => { /* PMT */ };
parser.OnBitrateMeasured += sample => { /* BitrateSample */ };
parser.OnParserComplete += () => { /* конец файла или StopParser() */ };

Logger.OnLogMessage += (status, message) =>
    Console.WriteLine($"[{status}] {message}");

parser.RunParser();
```

Для UDP:

```csharp
var config = new ParserConfig
{
    MulticastGroup = "239.0.0.1",
    MulticastPort = 1234,
    MulticastIncomingIp = null, // null → любой интерфейс
    CurrentDecodeMode = DecodeMode.Table,
};
```

Файл должен существовать и быть не меньше **2040** байт.

### PushBytes (DekTec и др.)

Для внешней подачи пакетов используйте **`TsParser(ParserConfig)`** с нужным `CurrentDecodeMode` и `CurrentTsMode`. Конструктор без параметров предназначен только для статических helper-методов; **`PushBytes` с ним приведёт к ошибке** — не инициализированы внутренние фабрики.

`RunParser()` не вызывайте — только подписка на события и цикл чтения:

```csharp
var config = new ParserConfig { CurrentDecodeMode = DecodeMode.Table };
using var parser = new TsParser(config);

parser.OnPatReady += /* ... */;

byte[] buffer = new byte[packetSize * 100];
while (!cancellationToken.IsCancellationRequested)
{
    int read = device.Read(buffer, buffer.Length);
    parser.PushBytes(buffer, packetSize); // packetSize: 188 или 204
}
```

Остановка: `parser.StopParser()` или `Dispose()`.

---

## Конфигурация (`ParserConfig`)

| Поле | Назначение |
|------|------------|
| `CurrentTsMode` | `DVB` — рабочий режим. `ATSC` / `ISDB` есть в enum, но фабрики таблиц выбрасывают `NotImplementedException` при первой SI-секции. |
| `CurrentDecodeMode` | `Table` — события SI-таблиц; `Packet` — только `OnTsPacketReady` (быстрее, без сборки секций). |
| `TsFileName` | Путь к `.ts`; размер ≥ 2040 байт. |
| `MulticastGroup`, `MulticastPort`, `MulticastIncomingIp` | UDP; порт по умолчанию **1234**, если `MulticastPort` не задан. |
| `ParserRunTime` | Лимит работы (мс), минимум **100** при установке. |
| `AllowAnalyzer` | CC и legacy `OnRate` по PID. |
| `BitrateMeasurement` | При `Enabled == true` анализатор включается **даже если** `AllowAnalyzer == false`; результаты в `OnBitrateMeasured`. |

На экземпляре `TsParser`:

- `EwsPidList` / `EewsPidList` — список PID для таблиц EWS/EEWS (иначе события не придут).
- `PacketSize` — `188` или `204` после разбора потока.

Класс `Decoder` в репозитории — заглушка, не используйте как готовый API.

---

## События

В режиме `DecodeMode.Table` подписывайтесь на нужные обработчики. Имена событий совпадают с публичным API (в т.ч. опечатка в коде).

| Событие | Тип данных | Примечание |
|---------|------------|------------|
| `OnPatReady` | `PAT` | |
| `OnPmtReady` | `PMT` | |
| `OnCatReady` | `CAT` | |
| `OnNitReady` | `NIT` | |
| `OnSdtReady` | `SDT` | |
| `OnBatReady` | `BAT` | |
| `OnEitReady` | `EIT` | |
| `OnTdtReady` | `TDT` | |
| `OnTotready` | `TOT` | Имя с маленькой **`r`** — `OnTotready`, не `OnTotReady`. |
| `OnAitReady` | `AIT` | PID из PMT |
| `OnMipReady` | `MIP` | PID `0x15` |
| `OnScte35Ready` | `SCTE35` | PID из PMT (`0x86`) |
| `OnEwsReady` | `EWS` | Нужен `EwsPidList` |
| `OnEewsReady` | `EEWS` | Нужен `EewsPidList` |
| `OnTsPacketReady` | `TsPacket` | Только `DecodeMode.Packet` |
| `OnRate` | `ushort pid, …` | Legacy-анализатор (`AllowAnalyzer`) |
| `OnBitrateMeasured` | `BitrateSample` | `BitrateMeasurement.Enabled` |
| `OnParserComplete` | — | Конец файла или `StopParser()` |

Логи: `Logger.OnLogMessage` (`TSParser.Service.Logger`). Неизвестные теги дескрипторов логируются **один раз на тег**.

Часть таблиц (RST, RNT, DIT, SIT и др.) разбирается, но отдельных событий нет — только запись в лог.

---

## Разбор без потока (статические методы)

| Метод | Назначение |
|-------|------------|
| `TsParser.GetOneTableFromBytes(bytes, mip: false)` | Одна SI-секция → `Table` (`mip: true` → `MIP`) |
| `TsParser.GetOneDescriptorFromBytes(bytes, callerTableId?)` | Один дескриптор; `table_id` `0x74` → AIT, `0xFC` → SCTE-35 splice |
| `TsParser.GetOneTsPacketFromBytes` / `GetTsPacketsFromBytes` | TS-пакеты |
| `TsParser.CompareTables(t1, t2)` | Структурное сравнение таблиц |

`table_id` для `GetOneTableFromBytes`: `0x00` PAT, `0x01` CAT, `0x02` PMT, `0x40`/`0x41` NIT, `0x42`/`0x46` SDT, `0x4A` BAT, `0x4E`–`0x6F` EIT, `0x70` TDT, `0x73` TOT, `0x74` AIT, `0x93` EWS, `0x94`/`0x95` EEWS, `0xFC` SCTE-35.

---

## Поддерживаемые таблицы

PAT, CAT, PMT, NIT, SDT, BAT, EIT, TDT, TOT, AIT, MIP, SCTE-35, EWS, EEWS.

В тестовом manifest для EEWS, EWS, MIP, SCTE35 могут быть пометки `missing: true` — типы в коде есть, корпус фикстур неполный.

---

## Дескрипторы

Полный dispatch — в [`DescriptorFactory.cs`](TSParser/Descriptors/DescriptorFactory.cs). Кратко по группам:

| Группа | Условие | Примеры тегов |
|--------|---------|----------------|
| **Custom** (операторские) | Проверяются первыми в `GetCustomDescriptor` | `0x09`, `0x86`–`0x90`, `0xB0`–`0xB2`, `0xB4`, `0xC0` |
| **DVB SI** | Стандартный `switch` | `0x02`, `0x03`, `0x05`, `0x48`, `0x4D`, `0x7F`, … |
| **Extension** | Тег `0x7F`, байт расширения | `0x00` ImageIcon, `0x04` T2 delivery |
| **AIT** | `table_id` `0x74` | `0x00`–`0x04`, `0x10`, `0x15` |
| **SCTE-35 splice** | `table_id` `0xFC` | `0x00`–`0x04` (avail, DTMF, segmentation, time, audio) |

Custom `0x09` перекрывает стандартный CA descriptor. Тег **`0xB3`** в factory не подключён (используйте **`0xB4`** — `TimeZoneDescriptorLG`). Неизвестные теги → базовый `Descriptor` + однократный лог.

Потребителям: разбор через граф таблиц или `GetOneDescriptorFromBytes`; `DescriptorFactory` — internal.

---

## Тестирование

Проект **TSParser.Tests** (NUnit), фикстуры в `TSParser.Tests/TestResources/` и manifest JSON.

```bash
dotnet test TSParser.Tests/TSParser.Tests.csproj
```

Переменная `TSPARSER_TEST_CORPUS` — альтернативный корень `TestResources` при `dotnet test`. Для CorpusHarvester/BlessManifest — `TSPARSER_TEST_FIXTURES` (см. [AGENTS.md](AGENTS.md)).

После добавления `.tbl` / `.desc` обновите manifest инструментом **BlessManifest** (CRC/hash).

---

## Инструменты разработчика

| Проект | Назначение |
|--------|------------|
| [tools/CorpusHarvester](tools/CorpusHarvester) | Сбор реальных TS → выборка дескрипторов для фикстур |
| [tools/BlessManifest](tools/BlessManifest) | Обновление `manifest.descriptors.json` / checksums |
| [TSParser.Benchmarks](TSParser.Benchmarks) | BenchmarkDotNet: `dotnet run --project TSParser.Benchmarks -c Release` |

---

## Планы развития

- Разбор NAL-единиц (`TransportStream/NAL/` — заготовка в проекте).
- Режимы **ATSC** / **ISDB** (сейчас только enum и throwing factories).
- PLP, teletext, субтитры, прямая поддержка адаптеров DekTec.

---

## Для разработчиков с AI

Подробный operational context для агентов (архитектура, правила правок, manifest workflow): **[AGENTS.md](AGENTS.md)** (English).

---

## Лицензия

Copyright Eldar Nizamutdinov. [Apache License 2.0](TSParser/LICENCE).
