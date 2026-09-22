using System.Buffers.Binary;
using NUnit.Framework;
using TSParser.Routing;
using TSParser.Service;
using TSParser.Tables.DvbTables;
using TSParser.Tests.Helpers;
using TSParser.TransportStream;

namespace TSParser.Tests.Tables;

[TestFixture]
public sealed class DynamicPidRegistryTests
{
    [Test]
    public void Registry_reconciles_all_program_routes_and_ignores_next_tables()
    {
        const ushort pmtPid = 0x0100;
        var registry = new DynamicPidRegistry(new T2miOptions
        {
            Enabled = true,
            AutoDetect = true,
            Pids = [0x1500],
        });
        registry.RegisterT2miPids([0x1500]);
        registry.UpdateFromPat(new PAT(BuildPat(current: true, version: 0, (1, pmtPid))));

        RoutePmt(registry, pmtPid, BuildPmt(true, 0, 1,
            (0x05, 0x0200, ApplicationSignallingDescriptor()),
            (0x05, 0x0201, ApplicationSignallingDescriptor()),
            (0x86, 0x0300, Array.Empty<byte>()),
            (0x86, 0x0301, Array.Empty<byte>())));

        Assert.Multiple(() =>
        {
            Assert.That(registry.IsTrackedPid(0x0200), Is.True);
            Assert.That(registry.IsTrackedPid(0x0201), Is.True);
            Assert.That(registry.IsTrackedPid(0x0300), Is.True);
            Assert.That(registry.IsTrackedPid(0x0301), Is.True);
        });

        RoutePmt(registry, pmtPid, BuildPmt(false, 1, 1, (0x86, 0x0400, Array.Empty<byte>())));
        Assert.That(registry.IsTrackedPid(0x0400), Is.False, "next PMT must not change active routes");

        RoutePmt(registry, pmtPid, BuildPmt(true, 1, 1, (0x86, 0x0400, Array.Empty<byte>())));
        Assert.Multiple(() =>
        {
            Assert.That(registry.IsTrackedPid(0x0200), Is.False);
            Assert.That(registry.IsTrackedPid(0x0300), Is.False);
            Assert.That(registry.IsTrackedPid(0x0400), Is.True);
        });

        registry.UpdateFromPat(new PAT(BuildPat(current: false, version: 2, (2, (ushort)0x0110))));
        Assert.That(registry.IsTrackedPid(pmtPid), Is.True, "next PAT must not replace the active program map");
    }

    [Test]
    public void Registry_removes_stale_auto_t2mi_but_preserves_explicit_pid()
    {
        const ushort pmtPid = 0x0100;
        var registry = new DynamicPidRegistry(new T2miOptions
        {
            Enabled = true,
            AutoDetect = true,
            Pids = [0x1500],
        });
        registry.RegisterT2miPids([0x1500]);
        registry.UpdateFromPat(new PAT(BuildPat(true, 0, (1, pmtPid))));

        RoutePmt(registry, pmtPid, BuildPmt(true, 0, 1, (0x06, 0x1000, Array.Empty<byte>())));
        Assert.That(registry.IsT2miPid(0x1000), Is.True);

        RoutePmt(registry, pmtPid, BuildPmt(true, 1, 1, (0x03, 0x1001, Array.Empty<byte>())));
        Assert.Multiple(() =>
        {
            Assert.That(registry.IsT2miPid(0x1000), Is.False);
            Assert.That(registry.IsT2miPid(0x1500), Is.True);
        });

        registry.UpdateFromPat(new PAT(BuildPat(true, 2)));
        Assert.That(registry.IsT2miPid(0x1500), Is.True);

        registry.EwsPidList = [0x1200];
        registry.EewsPidList = [0x1201];
        registry.ResetStreamState();
        Assert.Multiple(() =>
        {
            Assert.That(registry.IsT2miPid(0x1500), Is.True);
            Assert.That(registry.EwsPidList, Is.EqualTo(new ushort[] { 0x1200 }));
            Assert.That(registry.EewsPidList, Is.EqualTo(new ushort[] { 0x1201 }));
        });
    }

    private static void RoutePmt(DynamicPidRegistry registry, ushort pid, byte[] section)
    {
        var raw = PsiTsPacketFactory.BuildPsiTsPacket(pid, section);
        var packet = new TsPacketFactory().GetTsPackets(raw, 188)[0];
        registry.RouteDynamicTables(packet);
    }

    private static byte[] ApplicationSignallingDescriptor() => [0x6F, 0x03, 0x00, 0x00, 0x00];

    private static byte[] BuildPat(bool current, byte version, params (ushort Program, ushort Pid)[] programs)
    {
        var sectionLength = 9 + programs.Length * 4;
        var bytes = new byte[sectionLength + 3];
        bytes[0] = 0x00;
        bytes[1] = (byte)(0xB0 | (sectionLength >> 8));
        bytes[2] = (byte)sectionLength;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(3), 1);
        bytes[5] = (byte)(0xC0 | ((version & 0x1F) << 1) | (current ? 1 : 0));
        for (var i = 0; i < programs.Length; i++)
        {
            var offset = 8 + i * 4;
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset), programs[i].Program);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 2), (ushort)(0xE000 | programs[i].Pid));
        }
        WriteCrc(bytes);
        return bytes;
    }

    private static byte[] BuildPmt(bool current, byte version, ushort program, params (byte Type, ushort Pid, byte[] Descriptors)[] streams)
    {
        var esLength = streams.Sum(es => 5 + es.Descriptors.Length);
        var sectionLength = 13 + esLength;
        var bytes = new byte[sectionLength + 3];
        bytes[0] = 0x02;
        bytes[1] = (byte)(0xB0 | (sectionLength >> 8));
        bytes[2] = (byte)sectionLength;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(3), program);
        bytes[5] = (byte)(0xC0 | ((version & 0x1F) << 1) | (current ? 1 : 0));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(8), 0xFFFF);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(10), 0xF000);
        var offset = 12;
        foreach (var stream in streams)
        {
            bytes[offset] = stream.Type;
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 1), (ushort)(0xE000 | stream.Pid));
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset + 3), (ushort)(0xF000 | stream.Descriptors.Length));
            stream.Descriptors.CopyTo(bytes, offset + 5);
            offset += 5 + stream.Descriptors.Length;
        }
        WriteCrc(bytes);
        return bytes;
    }

    private static void WriteCrc(byte[] bytes)
    {
        var crc = Utils.GetCRC32(bytes.AsSpan(0, bytes.Length - 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(bytes.Length - 4), crc);
    }
}
