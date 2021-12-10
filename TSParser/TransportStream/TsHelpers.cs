namespace TSParser.TransportStream
{
    internal static class TsHelpers
    {
        internal static ulong GetPcrBase(ReadOnlySpan<byte> bytes)
        {
            ulong pcrBase = (ulong)bytes[0] << 25;
            pcrBase |= (ulong)bytes[1] << 17;
            pcrBase |= (ulong)bytes[2] << 9;
            pcrBase |= (ulong)bytes[3] << 1;
            pcrBase |= (0x80 & (ulong)bytes[4]) >> 7;
            return pcrBase;
        }
        internal static TimeSpan GetPcrTimeSpan(ulong pcr)
        {
            double t = (double)(pcr / 2.7);
            return TimeSpan.FromTicks((long)t);
        }
        internal static ushort GetPcrExtension(ReadOnlySpan<byte> bytes)
        {
            ushort pcrExtension = (ushort)((0x01 & (ushort)bytes[0]) << 8);
            pcrExtension |= (ushort)bytes[1];
            return pcrExtension;
        }
        internal static ulong GetPtsDts(ReadOnlySpan<byte> bytes)
        {
            ulong PTS_32_30 = (ulong)(bytes[0] >> 1) & 0x7;
            ulong PTS_29_15 = (ulong)(bytes[1] << 7) | (ulong)(bytes[2] >> 1);
            ulong PTS_14_0 = (ulong)(bytes[3] << 7) | (ulong)(bytes[4] >> 1);
            return (PTS_32_30 << 30) | (PTS_29_15 << 15) | PTS_14_0;
        }
        internal static uint GetEsRate(ReadOnlySpan<byte> bytes)
        {
            return (uint)(((bytes[0]) << 15) | (bytes[1] << 7) | (bytes[2] >> 1));
        }
        internal static TimeSpan GetPtsDtsValue(ulong hex)
        {
            double t = (double)(hex / 0.009);
            return TimeSpan.FromTicks((long)t);
        }
    }
}
