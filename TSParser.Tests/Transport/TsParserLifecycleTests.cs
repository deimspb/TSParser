// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NUnit.Framework;
using TSParser.Enums;

namespace TSParser.Tests.Transport;

[TestFixture]
public sealed class TsParserLifecycleTests
{
    [Test]
    public void RunParser_completes_twice_after_sequential_runs()
    {
        var path = WriteTempTs(packetCount: 12_000);
        try
        {
            var parser = new TsParser(new ParserConfig
            {
                TsFileName = path,
                CurrentDecodeMode = DecodeMode.Packet,
            });

            var completeCount = 0;
            parser.OnParserComplete += () => completeCount++;

            parser.RunParser();
            parser.RunParser();

            Assert.That(completeCount, Is.EqualTo(2));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task StopParser_on_one_instance_does_not_cancel_another()
    {
        var path = WriteTempTs(packetCount: 80_000);
        try
        {
            var parser1 = new TsParser(new ParserConfig
            {
                TsFileName = path,
                CurrentDecodeMode = DecodeMode.Packet,
            });
            var parser2 = new TsParser(new ParserConfig
            {
                TsFileName = path,
                CurrentDecodeMode = DecodeMode.Packet,
            });

            var parser2Completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            parser2.OnParserComplete += () => parser2Completed.TrySetResult();

            var run1 = parser1.RunParserAsync();
            var run2 = parser2.RunParserAsync();

            await Task.Delay(50);
            parser1.StopParser();
            await run1;

            var completed2 = await Task.WhenAny(parser2Completed.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.That(completed2, Is.SameAs(parser2Completed.Task), "Second parser should finish after the first is stopped");
            await run2;
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void PushBytes_invokes_packet_events_without_file_source()
    {
        var parser = new TsParser(new ParserConfig
        {
            CurrentDecodeMode = DecodeMode.Packet,
        });

        var packets = new List<TSParser.TransportStream.TsPacket>();
        parser.OnTsPacketReady += packets.Add;

        var bytes = new byte[10 * 188];
        for (var p = 0; p < 10; p++)
        {
            var offset = p * 188;
            bytes[offset] = 0x47;
            bytes[offset + 1] = 0x1F;
            bytes[offset + 2] = 0xFF;
        }

        parser.PushBytes(bytes, 188);

        Assert.That(packets, Has.Count.EqualTo(10));
    }

    [Test]
    public void Dispose_after_stop_does_not_throw()
    {
        var path = WriteTempTs(packetCount: 12_000);
        try
        {
            var parser = new TsParser(new ParserConfig
            {
                TsFileName = path,
                CurrentDecodeMode = DecodeMode.Packet,
            });

            var run = parser.RunParserAsync();
            parser.StopParser();
            run.Wait(TimeSpan.FromSeconds(10));

            Assert.DoesNotThrow(() =>
            {
                parser.Dispose();
                parser.Dispose();
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void RunParser_propagates_packet_event_exception()
    {
        var path = WriteTempTs(packetCount: 12_000);
        try
        {
            var parser = new TsParser(new ParserConfig
            {
                TsFileName = path,
                CurrentDecodeMode = DecodeMode.Packet,
            });

            parser.OnTsPacketReady += _ => throw new InvalidOperationException("packet handler failed");

            var ex = Assert.Throws<InvalidOperationException>(() => parser.RunParser());

            Assert.That(ex!.Message, Is.EqualTo("packet handler failed"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task StopParser_completes_once()
    {
        var path = WriteTempTs(packetCount: 80_000);
        try
        {
            var parser = new TsParser(new ParserConfig
            {
                TsFileName = path,
                CurrentDecodeMode = DecodeMode.Packet,
            });

            var completeCount = 0;
            parser.OnParserComplete += () => completeCount++;

            var run = parser.RunParserAsync();
            await Task.Delay(50);
            parser.StopParser();
            await run;

            Assert.That(completeCount, Is.EqualTo(1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempTs(int packetCount, int packetSize = 188)
    {
        var bytes = new byte[packetCount * packetSize];
        for (var p = 0; p < packetCount; p++)
        {
            var offset = p * packetSize;
            bytes[offset] = 0x47;
            bytes[offset + 1] = 0x1F;
            bytes[offset + 2] = 0xFF;
        }

        var path = Path.Combine(Path.GetTempPath(), $"tsparser-lifecycle-{Guid.NewGuid():N}.ts");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
