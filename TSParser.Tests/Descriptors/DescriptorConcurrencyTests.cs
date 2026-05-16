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
using TSParser.Descriptors;
using TSParser.Descriptors.AitDescriptors;
using TSParser.Descriptors.Scte35Descriptors;

namespace TSParser.Tests.Descriptors;

[TestFixture]
public sealed class DescriptorConcurrencyTests
{
    private static readonly byte[] DvbLoop = [0xFF, 0x00];
    private static readonly byte[] AitLoop = [0x00, 0x03, 0x00, 0x00, 0x00];
    private static readonly byte[] SpliceLoop = [0x00, 0x08, 0x00, 0x00, 0x00, 0x01, 0x11, 0x22, 0x33, 0x44];

    [Test]
    public void GetDescriptorList_parallel_resolvers_do_not_cross_contaminate()
    {
        var failures = 0;

        Parallel.For(0, 500, _ =>
        {
            try
            {
                switch (Random.Shared.Next(3))
                {
                    case 0:
                        var dvb = DescriptorFactory.GetDescriptorList(DvbLoop, "dvb");
                        Assert.That(dvb, Has.Count.EqualTo(1));
                        Assert.That(dvb[0].DescriptorTag, Is.EqualTo(0xFF));
                        Assert.That(dvb[0], Is.TypeOf<Descriptor>());
                        break;
                    case 1:
                        var ait = DescriptorFactory.GetDescriptorList(AitLoop, "ait", callerTableId: 0x74);
                        Assert.That(ait, Has.Count.EqualTo(1));
                        Assert.That(ait[0], Is.TypeOf<ApplicationDescriptor_0x00>());
                        break;
                    default:
                        var splice = DescriptorFactory.GetDescriptorList(SpliceLoop, "scte", callerTableId: 0xFC);
                        Assert.That(splice, Has.Count.EqualTo(1));
                        Assert.That(splice[0], Is.TypeOf<AvailDescriptor_0x00>());
                        break;
                }
            }
            catch
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.That(failures, Is.Zero);
    }
}
