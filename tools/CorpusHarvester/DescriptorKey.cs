namespace CorpusHarvester;

/// <summary>
/// Uniqueness key for a descriptor instance in the corpus.
/// </summary>
public readonly record struct DescriptorKey(
    string Family,
    byte Tag,
    byte? ExtensionTag,
    uint ContentCrc32)
{
    public string GroupDirectoryName =>
        ExtensionTag is byte ext
            ? $"{Family}_{Tag:X2}_ext{ext:X2}"
            : $"{Family}_{Tag:X2}";

    public string StagingFilePrefix =>
        ExtensionTag is byte ext
            ? $"{Family}_{Tag:X2}_ext{ext:X2}"
            : $"{Family}_{Tag:X2}";

    public static DescriptorKey FromRaw(ReadOnlySpan<byte> raw, byte? callerTableId, uint contentCrc32)
    {
        var family = callerTableId switch
        {
            0x74 => "AIT",
            0xFC => "SCTE",
            _ => "Dvb",
        };

        var tag = raw[0];
        byte? extTag = null;
        if (family == "Dvb" && tag == 0x7F && raw.Length > 2)
        {
            extTag = raw[2];
        }

        return new DescriptorKey(family, tag, extTag, contentCrc32);
    }
}
