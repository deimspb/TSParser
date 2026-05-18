using TSParser.Tests.Helpers;
using TSParser.TransportStream.T2mi;
var bytes = FixtureLoader.LoadBytes(FixtureLoader.T2miBundledRelativePath);
var asm = new T2miPacketAssembler();
var byType = new Dictionary<byte,int>();
asm.PacketReady += p => { var t=(byte)p.PacketType; byType[t]=byType.GetValueOrDefault(t)+1; };
for (var i=0;i+188<=bytes.Length;i+=188)
    asm.PushPacket(bytes.AsSpan(i,188), FixtureLoader.T2miSamplePid, (ulong)(i/188));
Console.WriteLine(string.Join(", ", byType.Select(kv=>$"0x{kv.Key:X2}={kv.Value}")));
