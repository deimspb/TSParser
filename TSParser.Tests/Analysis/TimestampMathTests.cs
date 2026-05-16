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

namespace TSParser.Tests.Analysis;

[TestFixture]
public sealed class TimestampMathTests
{
    [Test]
    public void Delta_without_wrap_returns_forward_difference()
    {
        var delta = TimestampMath.Delta(1_000, 50_000, TimestampMath.PtsDtsTimestampBitWidth);

        Assert.That(delta, Is.EqualTo(49_000UL));
    }

    [Test]
    public void Delta_at_33_bit_boundary_wraps_forward()
    {
        const ulong max33 = (1UL << 33) - 1;

        var delta = TimestampMath.Delta(max33, 1, TimestampMath.PtsDtsTimestampBitWidth);

        Assert.That(delta, Is.EqualTo(2UL));
    }

    [Test]
    public void Delta_at_42_bit_boundary_wraps_forward()
    {
        const ulong max42 = (1UL << 42) - 1;

        var delta = TimestampMath.Delta(max42, 1, TimestampMath.PcrTimestampBitWidth);

        Assert.That(delta, Is.EqualTo(2UL));
    }

    [Test]
    public void Delta_same_timestamp_returns_zero()
    {
        var delta = TimestampMath.Delta(123_456, 123_456, TimestampMath.PcrTimestampBitWidth);

        Assert.That(delta, Is.Zero);
    }

    [Test]
    public void BitsPerSecond_uses_transport_byte_formula()
    {
        const ulong bytes = 1_880_000;
        const ulong deltaTicks = TimestampMath.PcrTickRate;
        const double expectedBps = bytes * 8.0;

        var bps = TimestampMath.BitsPerSecond(bytes, deltaTicks, TimestampMath.PcrTickRate);

        Assert.That(bps, Is.EqualTo(expectedBps).Within(0.001));
    }

    [Test]
    public void WindowDuration_converts_ticks_to_time_span()
    {
        var duration = TimestampMath.WindowDuration(TimestampMath.PcrTickRate / 10, TimestampMath.PcrTickRate);

        Assert.That(duration.TotalMilliseconds, Is.EqualTo(100.0).Within(0.001));
    }
}
