namespace TSParser.Web.Services;

internal static class BitrateSumBuilder
{
    private static readonly long UdpBucketTicks = TimeSpan.FromSeconds(1).Ticks;

    public static IReadOnlyList<BitratePidFilePoint> FromFilePidSeries(
        IReadOnlyList<ushort> chartPids,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidFilePoint>> pidFile)
    {
        var byOffset = new SortedDictionary<long, double>();

        foreach (var pid in chartPids)
        {
            if (!pidFile.TryGetValue(pid, out var points))
                continue;

            foreach (var point in points)
                byOffset[point.ByteOffset] = byOffset.GetValueOrDefault(point.ByteOffset) + point.BitsPerSecond;
        }

        if (byOffset.Count == 0)
            return [];

        return byOffset.Select(kv => new BitratePidFilePoint(kv.Key, kv.Value)).ToArray();
    }

    public static IReadOnlyList<BitratePidUdpPoint> FromUdpPidSeries(
        IReadOnlyList<ushort> chartPids,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidUdpPoint>> pidUdp)
    {
        var byBucket = new SortedDictionary<long, (DateTime Timestamp, double BitsPerSecond)>();

        foreach (var pid in chartPids)
        {
            if (!pidUdp.TryGetValue(pid, out var points))
                continue;

            foreach (var point in points)
            {
                var bucket = point.TimestampUtc.Ticks / UdpBucketTicks;
                if (byBucket.TryGetValue(bucket, out var existing))
                    byBucket[bucket] = (existing.Timestamp, existing.BitsPerSecond + point.BitsPerSecond);
                else
                    byBucket[bucket] = (point.TimestampUtc, point.BitsPerSecond);
            }
        }

        if (byBucket.Count == 0)
            return [];

        return byBucket.Values.Select(v => new BitratePidUdpPoint(v.Timestamp, v.BitsPerSecond)).ToArray();
    }
}
