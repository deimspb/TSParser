namespace TSParser;

/// <summary>Limits and destination for recording raw UDP multicast datagrams.</summary>
public sealed record UdpRecordingOptions
{
    public required string FilePath { get; init; }
    public TimeSpan? MaxDuration { get; init; }
    public long? MaxBytes { get; init; }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);
        if (MaxDuration is { } duration && duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaxDuration), "Duration limit must be positive.");
        if (MaxBytes is { } bytes && bytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxBytes), "Byte limit must be positive.");
        if ((MaxDuration is null) == (MaxBytes is null))
            throw new ArgumentException("Specify exactly one recording limit: time or bytes.", nameof(UdpRecordingOptions));
    }
}

public enum UdpRecordingStopReason
{
    StoppedByUser,
    TimeLimitReached,
    ByteLimitReached,
    SourceStopped,
    Error
}

/// <summary>Final result emitted once for a UDP recording.</summary>
public sealed record UdpRecordingResult(
    string FilePath,
    long BytesWritten,
    TimeSpan Duration,
    UdpRecordingStopReason Reason,
    Exception? Error = null);

public readonly record struct UdpRecordingStatus(string FilePath, long BytesWritten, TimeSpan Elapsed);
