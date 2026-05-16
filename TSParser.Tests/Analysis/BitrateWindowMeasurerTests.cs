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
using TSParser.Analysis;
using TSParser.Analysis.Metric;

namespace TSParser.Tests.Analysis;

[TestFixture]
public sealed class BitrateWindowMeasurerTests
{
    [Test]
    public void OnTimestamp_emits_sample_when_window_ticks_elapsed()
    {
        const int packetSize = 188;
        const int packetCount = 1000;
        const ulong windowTicks = TimestampMath.PcrTickRate;
        var measurer = new BitrateWindowMeasurer(
            windowTicks,
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTimestampBitWidth,
            BitrateClockSource.Pcr,
            pid: 0x100);

        Assert.That(measurer.OnTimestamp(0), Is.Null);

        for (var i = 0; i < packetCount; i++)
            measurer.AddBytes(packetSize);

        var sample = measurer.OnTimestamp(windowTicks);

        Assert.That(sample, Is.Not.Null);
        Assert.That(sample!.Value.Pid, Is.EqualTo(0x100));
        Assert.That(sample.Value.BytesInWindow, Is.EqualTo((ulong)(packetSize * packetCount)));
        Assert.That(sample.Value.ClockSource, Is.EqualTo(BitrateClockSource.Pcr));
        Assert.That(sample.Value.WindowDuration.TotalSeconds, Is.EqualTo(1.0).Within(0.0001));
        Assert.That(
            sample.Value.BitsPerSecond,
            Is.EqualTo(packetSize * packetCount * 8.0).Within(0.001));
    }

    [Test]
    public void OnTimestamp_does_not_emit_before_window_is_full()
    {
        var measurer = new BitrateWindowMeasurer(
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTimestampBitWidth,
            BitrateClockSource.Pcr);

        measurer.OnTimestamp(0);
        measurer.AddBytes(188);

        var sample = measurer.OnTimestamp(TimestampMath.PcrTickRate / 2);

        Assert.That(sample, Is.Null);
    }

    [Test]
    public void OnTimestamp_does_not_emit_without_bytes()
    {
        var measurer = new BitrateWindowMeasurer(
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTimestampBitWidth,
            BitrateClockSource.Pcr);

        measurer.OnTimestamp(0);

        var sample = measurer.OnTimestamp(TimestampMath.PcrTickRate);

        Assert.That(sample, Is.Null);
    }

    [Test]
    public void OnTimestamp_handles_pts_wrap_at_33_bits()
    {
        const ulong max33 = (1UL << 33) - 1;
        const ulong windowTicks = 2;
        var measurer = new BitrateWindowMeasurer(
            windowTicks,
            TimestampMath.PtsDtsTickRate,
            TimestampMath.PtsDtsTimestampBitWidth,
            BitrateClockSource.Pts);

        measurer.OnTimestamp(max33);
        measurer.AddBytes(900);

        var sample = measurer.OnTimestamp(1);

        Assert.That(sample, Is.Not.Null);
        Assert.That(sample!.Value.BytesInWindow, Is.EqualTo(900UL));
        Assert.That(sample.Value.WindowDuration, Is.EqualTo(TimestampMath.WindowDuration(2, TimestampMath.PtsDtsTickRate)));
        Assert.That(sample.Value.BitsPerSecond, Is.EqualTo(900 * 8.0 * TimestampMath.PtsDtsTickRate / 2).Within(0.001));
    }

    [Test]
    public void AssumedTransportRate_emits_at_nominal_bitrate_when_window_fills()
    {
        const int packetSize = 188;
        const double assumedBps = 10_000_000;
        const ulong windowTicks = TimestampMath.PcrTickRate;
        const int packetCount = 7000;

        var measurer = new BitrateWindowMeasurer(
            windowTicks,
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTimestampBitWidth,
            BitrateClockSource.AssumedTransportRate,
            assumedBitsPerSecond: assumedBps);

        BitrateSample? sample = null;
        for (var i = 0; i < packetCount && sample == null; i++)
            sample = measurer.AddBytes(packetSize);

        Assert.That(sample, Is.Not.Null);
        Assert.That(sample!.Value.ClockSource, Is.EqualTo(BitrateClockSource.AssumedTransportRate));
        Assert.That(sample.Value.BitsPerSecond, Is.EqualTo(assumedBps).Within(assumedBps * 0.02));
        Assert.That(sample.Value.WindowDuration.TotalSeconds, Is.EqualTo(1.0).Within(0.02));
        Assert.That(sample.Value.BytesInWindow, Is.GreaterThan(0));
    }

    [Test]
    public void AssumedTransportRate_does_not_emit_before_virtual_window_elapses()
    {
        const int packetSize = 188;
        const double assumedBps = 10_000_000;
        const ulong windowTicks = TimestampMath.PcrTickRate;

        var measurer = new BitrateWindowMeasurer(
            windowTicks,
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTimestampBitWidth,
            BitrateClockSource.AssumedTransportRate,
            assumedBitsPerSecond: assumedBps);

        for (var i = 0; i < 100; i++)
        {
            var sample = measurer.AddBytes(packetSize);
            Assert.That(sample, Is.Null);
        }
    }

    [Test]
    public void ResetWindow_clears_accumulated_state()
    {
        var measurer = new BitrateWindowMeasurer(
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTickRate,
            TimestampMath.PcrTimestampBitWidth,
            BitrateClockSource.Pcr);

        measurer.OnTimestamp(0);
        measurer.AddBytes(188);
        measurer.ResetWindow();

        var sample = measurer.OnTimestamp(TimestampMath.PcrTickRate);

        Assert.That(sample, Is.Null);
    }
}
