namespace CorpusHarvester;

internal static class DescriptorLoopWalker
{
    public static void Walk(ReadOnlySpan<byte> loop, byte? callerTableId, Action<ReadOnlySpan<byte>, DescriptorKey> onDescriptor)
    {
        var pointer = 0;
        while (pointer < loop.Length)
        {
            var remaining = loop[pointer..];
            if (remaining.Length < 2)
            {
                break;
            }

            var headerLen = 2;
            var lengthIndex = 1;
            var tag = remaining[0];
            if ((tag == 0x89 || tag == 0x90) && remaining.Length > 2 && remaining[1] == 0x15)
            {
                headerLen = 3;
                lengthIndex = 2;
            }

            if (remaining.Length <= lengthIndex)
            {
                break;
            }

            var descriptorLength = remaining[lengthIndex];
            var total = headerLen + descriptorLength;
            if (total <= 0 || pointer + total > loop.Length)
            {
                break;
            }

            var raw = loop.Slice(pointer, total);
            var crc = Crc32Helper.Compute(raw);
            var key = DescriptorKey.FromRaw(raw, callerTableId, crc);
            onDescriptor(raw, key);
            pointer += total;
        }
    }
}
