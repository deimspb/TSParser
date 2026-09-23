using NUnit.Framework;
using TSParser.Input;

namespace TSParser.Tests.Transport;

[TestFixture]
public sealed class UdpDatagramRecorderTests
{
    [Test]
    public async Task Byte_limit_never_writes_a_partial_datagram()
    {
        var path = NewTempPath();
        try
        {
            using var recorder = new UdpDatagramRecorder();
            var completion = CreateCompletion(recorder);
            recorder.Start(new UdpRecordingOptions { FilePath = path, MaxBytes = 10 });

            recorder.Record(new byte[] { 1, 2, 3, 4, 5, 6 });
            recorder.Record(new byte[] { 7, 8, 9, 10, 11, 12 });
            var completed = await completion.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6 }));
                Assert.That(completed.BytesWritten, Is.EqualTo(6));
                Assert.That(completed.Reason, Is.EqualTo(UdpRecordingStopReason.ByteLimitReached));
                Assert.That(recorder.IsRecording, Is.False);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Start_uses_create_new_and_preserves_an_existing_file()
    {
        var path = NewTempPath();
        File.WriteAllText(path, "existing");
        try
        {
            using var recorder = new UdpDatagramRecorder();

            Assert.Throws<IOException>(() => recorder.Start(
                new UdpRecordingOptions { FilePath = path, MaxBytes = 100 }));
            Assert.That(File.ReadAllText(path), Is.EqualTo("existing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Time_limit_stops_even_when_no_datagrams_arrive()
    {
        var path = NewTempPath();
        try
        {
            using var recorder = new UdpDatagramRecorder();
            var completion = CreateCompletion(recorder);
            recorder.Start(new UdpRecordingOptions { FilePath = path, MaxDuration = TimeSpan.FromMilliseconds(50) });

            var result = await completion.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(result.Reason, Is.EqualTo(UdpRecordingStopReason.TimeLimitReached));
                Assert.That(result.BytesWritten, Is.Zero);
                Assert.That(recorder.IsRecording, Is.False);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Options_require_exactly_one_limit()
    {
        var path = NewTempPath();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => new UdpRecordingOptions { FilePath = path }.Validate());
            Assert.Throws<ArgumentException>(() => new UdpRecordingOptions
            {
                FilePath = path,
                MaxBytes = 100,
                MaxDuration = TimeSpan.FromSeconds(1)
            }.Validate());
        });
    }

    [Test]
    public async Task Manual_stop_drains_accepted_datagrams_in_order_and_repeated_stop_is_idempotent()
    {
        var path = NewTempPath();
        try
        {
            using var recorder = new UdpDatagramRecorder();
            var completion = CreateCompletion(recorder, out var completionCount);
            recorder.Start(new UdpRecordingOptions { FilePath = path, MaxBytes = 100 });

            recorder.Record(new byte[] { 1, 2 });
            recorder.Record(new byte[] { 3, 4 });
            recorder.Stop();
            recorder.Stop();

            var result = await completion.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
                Assert.That(result.Reason, Is.EqualTo(UdpRecordingStopReason.StoppedByUser));
                Assert.That(result.BytesWritten, Is.EqualTo(4));
                Assert.That(completionCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Queue_overflow_stops_with_error_and_drains_already_accepted_datagrams()
    {
        var stream = new BlockingWriteStream();
        var recorder = new UdpDatagramRecorder(1, _ => stream);
        try
        {
            var completion = CreateCompletion(recorder, out var completionCount);
            recorder.Start(new UdpRecordingOptions { FilePath = NewTempPath(), MaxBytes = 100 });

            recorder.Record(new byte[] { 1 });
            await stream.FirstWriteStarted.WaitAsync(TimeSpan.FromSeconds(2));
            recorder.Record(new byte[] { 2 });
            recorder.Record(new byte[] { 3 });
            recorder.Stop();
            stream.Release();

            var result = await completion.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Multiple(() =>
            {
                Assert.That(stream.Bytes, Is.EqualTo(new byte[] { 1, 2 }));
                Assert.That(result.Reason, Is.EqualTo(UdpRecordingStopReason.Error));
                Assert.That(result.Error?.Message, Does.Contain("queue capacity"));
                Assert.That(result.BytesWritten, Is.EqualTo(2));
                Assert.That(completionCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            stream.Release();
            recorder.Dispose();
        }
    }

    private static Task<UdpRecordingResult> CreateCompletion(UdpDatagramRecorder recorder) =>
        CreateCompletion(recorder, out _);

    private static Task<UdpRecordingResult> CreateCompletion(
        UdpDatagramRecorder recorder,
        out Func<int> completionCount)
    {
        var count = 0;
        var source = new TaskCompletionSource<UdpRecordingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.Completed += result =>
        {
            Interlocked.Increment(ref count);
            source.TrySetResult(result);
        };
        completionCount = () => Volatile.Read(ref count);
        return source.Task;
    }

    private static string NewTempPath() =>
        Path.Combine(TestContext.CurrentContext.WorkDirectory, $"udp-recording-{Guid.NewGuid():N}.ts");

    private sealed class BlockingWriteStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly TaskCompletionSource _firstWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCount;

        public Task FirstWriteStarted => _firstWriteStarted.Task;
        public byte[] Bytes => _inner.ToArray();

        public void Release() => _release.TrySetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _writeCount) == 1)
            {
                _firstWriteStarted.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }

            await _inner.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }
}
