using TSParser.Analysis;

namespace TSParser.Desktop.Services;

public enum Tr101290ViewFilter
{
    All,
    ErrorsOnly,
    Priority1,
    Priority2,
    Priority3
}

public sealed record Tr101290ErrorRow(
    Tr101290Priority Priority,
    string PriorityText,
    Tr101290Indicator Indicator,
    string IndicatorName,
    string State,
    bool IsError,
    ulong Count,
    string PidText,
    ulong PacketNumber,
    string Detail);

public sealed class Tr101290Snapshot
{
    public static readonly Tr101290Snapshot Empty = new(0, 0, 0, [], []);

    public Tr101290Snapshot(
        int activePriority1,
        int activePriority2,
        int activePriority3,
        IReadOnlyList<Tr101290ErrorRow> rows,
        IReadOnlyList<string> journal)
    {
        ActivePriority1 = activePriority1;
        ActivePriority2 = activePriority2;
        ActivePriority3 = activePriority3;
        Rows = rows;
        Journal = journal;
    }

    public int ActivePriority1 { get; }
    public int ActivePriority2 { get; }
    public int ActivePriority3 { get; }
    public IReadOnlyList<Tr101290ErrorRow> Rows { get; }
    public IReadOnlyList<string> Journal { get; }

    public string Summary =>
        $"P1 {ActivePriority1}    P2 {ActivePriority2}    P3 {ActivePriority3}";
}

/// <summary>UI projection of TR 101 290 events. The journal keeps the latest 500 lines.</summary>
public sealed class Tr101290ErrorStore
{
    public const int JournalCapacity = 500;

    private readonly object _lock = new();
    private readonly Dictionary<(Tr101290Indicator Indicator, ushort Pid, bool HasPid), Tr101290ErrorRow> _rows = new();
    private readonly LinkedList<string> _journal = new();
    private int _revision;

    public int Revision
    {
        get
        {
            lock (_lock)
                return _revision;
        }
    }

    public int ActivePriority1 { get; private set; }
    public int ActivePriority2 { get; private set; }
    public int ActivePriority3 { get; private set; }

    public void Apply(Tr101290Event measurement)
    {
        lock (_lock)
        {
            var key = (measurement.Indicator, measurement.Pid ?? (ushort)0, measurement.Pid.HasValue);
            _rows.TryGetValue(key, out var previous);
            var isError = measurement.Kind == Tr101290EventKind.Raised;
            var row = new Tr101290ErrorRow(
                measurement.Priority,
                ((int)measurement.Priority).ToString(),
                measurement.Indicator,
                Tr101290Names.Indicator(measurement.Indicator),
                isError ? "Error" : "OK",
                isError,
                measurement.OccurrenceCount,
                measurement.Pid is ushort pid ? $"0x{pid:X4}" : "",
                measurement.PacketNumber,
                measurement.Detail);
            _rows[key] = row;

            if (isError || previous is { IsError: true })
            {
                var linePid = string.IsNullOrEmpty(row.PidText) ? "" : $" {row.PidText}";
                var verb = isError ? "error" : "cleared";
                _journal.AddFirst($"#{measurement.PacketNumber} {row.IndicatorName}{linePid} {verb}: {measurement.Detail}");
                while (_journal.Count > JournalCapacity)
                    _journal.RemoveLast();
            }

            Recount();
            _revision++;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _rows.Clear();
            _journal.Clear();
            ActivePriority1 = 0;
            ActivePriority2 = 0;
            ActivePriority3 = 0;
            _revision++;
        }
    }

    public Tr101290Snapshot GetSnapshot(Tr101290ViewFilter filter)
    {
        lock (_lock)
        {
            var rows = _rows.Values
                .Where(row => Matches(row, filter))
                .OrderBy(row => row.Priority)
                .ThenBy(row => row.IndicatorName, StringComparer.Ordinal)
                .ThenBy(row => row.PidText, StringComparer.Ordinal)
                .ToArray();

            return new Tr101290Snapshot(
                ActivePriority1,
                ActivePriority2,
                ActivePriority3,
                rows,
                _journal.ToArray());
        }
    }

    private void Recount()
    {
        ActivePriority1 = 0;
        ActivePriority2 = 0;
        ActivePriority3 = 0;
        foreach (var row in _rows.Values)
        {
            if (!row.IsError)
                continue;

            switch (row.Priority)
            {
                case Tr101290Priority.First:
                    ActivePriority1++;
                    break;
                case Tr101290Priority.Second:
                    ActivePriority2++;
                    break;
                default:
                    ActivePriority3++;
                    break;
            }
        }
    }

    private static bool Matches(Tr101290ErrorRow row, Tr101290ViewFilter filter) => filter switch
    {
        Tr101290ViewFilter.ErrorsOnly => row.IsError,
        Tr101290ViewFilter.Priority1 => row.Priority == Tr101290Priority.First,
        Tr101290ViewFilter.Priority2 => row.Priority == Tr101290Priority.Second,
        Tr101290ViewFilter.Priority3 => row.Priority == Tr101290Priority.Third,
        _ => true,
    };
}
