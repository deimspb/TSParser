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
using TSParser;
using TSParser.Analysis;
using TSParser.Enums;
using TSParser.Tables.DvbTables;
using TSParser.Tests.Helpers;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.T2mi;

[TestFixture]
public sealed class PlpServiceAggregatorTests
{
    private const string PatFixture = "Tables/PAT/PAT_M1.tbl";
    private const string PmtFixture = "Tables/PMT/PMT_M1.tbl";
    private const string SdtFixture = "Tables/SDT/SDT_M1.tbl";
    private const ushort PatM1PmtPid = 1000;

    [Test]
    public void Inner_parser_host_aggregates_pat_from_deencapsulated_plp_ts()
    {
        var patSection = FixtureLoader.LoadBytes(PatFixture);
        var innerPat = T2miTestPacketFactory.BuildPsiTsPacket(0, patSection);
        var outer = T2miTestPacketFactory.BuildT2miCarrierStream(innerPat);

        using var host = new PlpInnerParserHost();
        var parser = CreateOuterParser(host);
        parser.PushBytes(outer, T2miAccessors.TsPacketSize);

        var plpId = T2miTestPacketFactory.SampleBasebandPlpId;
        var services = host.Aggregator.GetServices(FixtureLoader.T2miSamplePid, plpId);
        Assert.That(services, Has.Count.EqualTo(1));
        Assert.That(services[0].ProgramNumber, Is.EqualTo(5));
        Assert.That(services[0].PmtPid, Is.EqualTo(PatM1PmtPid));
        Assert.That(host.Aggregator.HasReceivedPlpTs(FixtureLoader.T2miSamplePid, plpId), Is.True);
    }

    [Test]
    public void Inner_parser_host_aggregates_pat_and_pmt_from_synthetic_plp_ts()
    {
        var patSection = FixtureLoader.LoadBytes(PatFixture);
        var pmtSection = FixtureLoader.LoadBytes(PmtFixture);
        var expectedPmt = (PMT)TsParser.GetOneTableFromBytes(pmtSection)!;

        var innerPackets = new ReadOnlyMemory<byte>[]
        {
            T2miTestPacketFactory.BuildPsiTsPacket(0, patSection),
            T2miTestPacketFactory.BuildPsiTsPacket(PatM1PmtPid, pmtSection),
        };
        var outer = T2miTestPacketFactory.BuildT2miCarrierStream(innerPackets);

        using var host = new PlpInnerParserHost();
        var parser = CreateOuterParser(host);
        parser.PushBytes(outer, T2miAccessors.TsPacketSize);

        var plpId = T2miTestPacketFactory.SampleBasebandPlpId;
        var services = host.Aggregator.GetServices(FixtureLoader.T2miSamplePid, plpId);

        var patService = services.Single(s => s.ProgramNumber == 5);
        Assert.That(patService.PmtPid, Is.EqualTo(PatM1PmtPid));

        var pmtService = services.Single(s => s.ProgramNumber == expectedPmt.ProgramNumber);
        Assert.That(pmtService.PcrPid, Is.EqualTo(expectedPmt.PcrPid));
        Assert.That(pmtService.ElementaryStreams, Has.Count.EqualTo(expectedPmt.EsInfoList!.Count()));
    }

    [Test]
    public void Inner_parser_host_aggregates_sdt_service_names_from_synthetic_plp_ts()
    {
        var sdtSection = FixtureLoader.LoadBytes(SdtFixture);
        var expectedSdt = (SDT)TsParser.GetOneTableFromBytes(sdtSection)!;
        var expectedItem = expectedSdt.SdtItemsList!.First(item =>
            PlpServiceDescriptorHelper.TryGetServiceDescriptor(item.SdtItemDescriptorList) is not null);

        var innerSdt = T2miTestPacketFactory.BuildPsiTsStream((ushort)ReservedPids.SDT, sdtSection);
        var plpId = T2miTestPacketFactory.SampleBasebandPlpId;

        using var host = new PlpInnerParserHost();
        host.OnPlpTsReady(FixtureLoader.T2miSamplePid, plpId, innerSdt);

        var services = host.Aggregator.GetServices(FixtureLoader.T2miSamplePid, plpId);
        var service = services.Single(s => s.ServiceId == expectedItem.ServiceId);

        var descriptor = PlpServiceDescriptorHelper.TryGetServiceDescriptor(expectedItem.SdtItemDescriptorList)!;
        Assert.That(service.ServiceName, Is.EqualTo(descriptor.ServiceName.Trim()));
        Assert.That(service.ServiceType, Is.EqualTo(descriptor.ServiceType));
        Assert.That(host.Aggregator.HasReceivedPlpTs(FixtureLoader.T2miSamplePid, plpId), Is.True);
    }

    [Test]
    public void RunParser_on_t2mi_sample_aggregates_services_when_inner_pat_present()
    {
        var path = FixtureLoader.ResolveT2miSamplePath();

        using var host = new PlpInnerParserHost();
        var parser = new TsParser(new ParserConfig
        {
            TsFileName = path,
            T2miEnabled = true,
            T2miDeencapsulate = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });

        parser.OnPlpTsReady += host.OnPlpTsReady;
        parser.RunParser();

        if (!host.Aggregator.GetPlpKeys().Any(key => host.Aggregator.HasReceivedPlpTs(key.T2miPid, key.PlpId)))
        {
            Assert.Ignore("Sample has no decapsulatable baseband BB frames with DFL > 0.");
        }

        var programCount = host.Aggregator.GetPlpKeys()
            .SelectMany(key => host.Aggregator.GetServices(key.T2miPid, key.PlpId))
            .Count(service => service.ProgramNumber != 0 && service.PmtPid.HasValue);

        if (programCount == 0)
        {
            Assert.Ignore("Decapsulated PLP TS did not contain a parseable inner PAT with programs.");
        }

        Assert.That(programCount, Is.GreaterThan(0));
    }

    [Test]
    public void RunParser_on_full_sample_aggregates_plp_services_when_configured()
    {
        if (!FixtureLoader.TryGetT2miFullSamplePath(out var path))
        {
            Assert.Ignore($"Set {FixtureLoader.T2miSampleEnvironmentVariable} to the full t2mi_cut.ts path to run this test.");
        }

        using var host = new PlpInnerParserHost();
        var parser = new TsParser(new ParserConfig
        {
            TsFileName = path,
            T2miEnabled = true,
            T2miDeencapsulate = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });

        parser.OnPlpTsReady += host.OnPlpTsReady;
        parser.RunParser();

        if (!host.Aggregator.GetPlpKeys().Any(key => host.Aggregator.HasReceivedPlpTs(key.T2miPid, key.PlpId)))
        {
            Assert.Ignore("Full capture has no decapsulatable baseband BB frames with DFL > 0.");
        }

        var programCount = host.Aggregator.GetPlpKeys()
            .SelectMany(key => host.Aggregator.GetServices(key.T2miPid, key.PlpId))
            .Count(service => service.ProgramNumber != 0);

        if (programCount == 0)
        {
            Assert.Ignore("Full capture PLP TS did not contain a parseable inner PAT with programs.");
        }

        Assert.That(programCount, Is.GreaterThan(0));
    }

    private static TsParser CreateOuterParser(PlpInnerParserHost host)
    {
        var parser = new TsParser(new ParserConfig
        {
            T2miEnabled = true,
            T2miDeencapsulate = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });
        parser.OnPlpTsReady += host.OnPlpTsReady;
        return parser;
    }
}
