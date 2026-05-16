using TSParser;
using TSParser.Enums;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
namespace CorpusHarvester;

internal sealed class TsCorpusHarvester
{
    private readonly DescriptorCollector _collector;
    private readonly int _runTimeMs;
    private string _currentSource = string.Empty;

    public TsCorpusHarvester(DescriptorCollector collector, int runTimeMs)
    {
        _collector = collector;
        _runTimeMs = runTimeMs;
    }

    public void HarvestFile(string tsPath)
    {
        _currentSource = Path.GetFileName(tsPath);
        Console.WriteLine($"  >> {_currentSource}");

        var config = new ParserConfig
        {
            TsFileName = tsPath,
            CurrentDecodeMode = DecodeMode.Table,
            ParserRunTime = _runTimeMs,
            AllowAnalyzer = true,
        };

        var parser = new TsParser(config);
        WireEvents(parser);
        parser.RunParser();
    }

    private void WireEvents(TsParser parser)
    {
        parser.OnCatReady += t => Collect(t);
        parser.OnPmtReady += t => Collect(t);
        parser.OnNitReady += t => Collect(t);
        parser.OnSdtReady += t => Collect(t);
        parser.OnBatReady += t => Collect(t);
        parser.OnEitReady += t => Collect(t);
        parser.OnTotready += t => Collect(t);
        parser.OnAitReady += t => Collect(t);
        parser.OnScte35Ready += t => Collect(t);
        parser.OnEwsReady += t => Collect(t);
        parser.OnEewsReady += t => Collect(t);

        parser.OnPatReady += pat =>
        {
            if (parser.PidList.Count > 0)
            {
                TrySetEwsPids(parser, pat);
            }
        };
    }

    private static void TrySetEwsPids(TsParser parser, PAT pat)
    {
        var ewsPids = parser.PidList
            .Where(pid => pid is >= 0x100 and <= 0x1FFF)
            .Distinct()
            .Take(32)
            .ToList();

        if (ewsPids.Count > 0 && parser.EwsPidList.Count == 0)
        {
            parser.EwsPidList = ewsPids;
            parser.EewsPidList = ewsPids;
        }
    }

    private void Collect(Table table) => _collector.CollectFromTable(table, _currentSource);
}
