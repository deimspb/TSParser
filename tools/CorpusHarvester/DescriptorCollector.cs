using System.Collections.Concurrent;
using TSParser.Tables;

namespace CorpusHarvester;

internal sealed class DescriptorCollector
{
    private readonly string _stagingRoot;
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.OrdinalIgnoreCase);
    private int _savedCount;

    public DescriptorCollector(string stagingRoot)
    {
        _stagingRoot = stagingRoot;
        Directory.CreateDirectory(stagingRoot);
    }

    public int UniqueCount => _seen.Count;
    public int SavedCount => _savedCount;

    public void CollectFromTable(Table table, string sourceTs)
    {
        TableDescriptorLoops.Visit(table, (loop, callerTableId) =>
        {
            DescriptorLoopWalker.Walk(loop, callerTableId, (raw, key) => TrySave(raw, key, sourceTs));
        });
    }

    private void TrySave(ReadOnlySpan<byte> raw, DescriptorKey key, string sourceTs)
    {
        var dedupKey = $"{key.GroupDirectoryName}:{key.ContentCrc32:X8}";
        if (!_seen.TryAdd(dedupKey, 0))
        {
            return;
        }

        var groupDir = Path.Combine(_stagingRoot, key.GroupDirectoryName);
        Directory.CreateDirectory(groupDir);

        var crc8 = Crc32Helper.Crc8Hex(key.ContentCrc32);
        var fileName = $"{key.StagingFilePrefix}_{crc8}_{raw.Length}.desc";
        var path = Path.Combine(groupDir, fileName);

        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllBytes(path, raw.ToArray());
        Interlocked.Increment(ref _savedCount);
    }
}
