using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSParser.TransportStream
{
    public readonly struct AdaptationField
    {
        public byte AdaptationFieldLength { get; init; }
        public bool DiscontinuityIndicator { get; init; }
        public bool RandomAccessIndicator { get; init; }
        public bool ElementaryStreamPriorityIndicator { get; init; }
        public bool PCRFlag { get; init; }
        public bool OPCRFlag { get; init; }
        public bool SplicingPointFlag { get; init; }
        public bool TransportPrivateDataFlag { get; init; }
        public bool AdaptationFieldExtensionFlag { get; init; }
        public ulong ProgramClockReferenceBase { get; init; }
        public ushort ProgramClockReferenceExtension { get; init; }
        public ulong OriginalProgramClockReferenceBase { get; init; }
        public ushort OriginalProgramClockReferenceExtension { get; init; }
        public byte SpliceCountdown { get; init; }
        public byte TransportPrivateDataLength { get; init; }
        public byte[] PrivateDataByte { get; init; }
        public byte AdaptationFieldExtensionLength { get; init; }
        public bool LtwFlag { get; init; }
        public bool PiecewiseRateFlag { get; init; }
        public bool SeamlessSpliceFlag { get; init; }
        public bool LtwValidFlag { get; init; }
        public ushort LtwOffset { get; init; }
        public uint PiecewiseRate { get; init; }
        public byte SpliceType { get; init; }
        public ulong DTSNext_AU { get; init; }
        public ulong PcrValue { get; init; }
        public TimeSpan PcrTime => TsHelpers.GetPcrTimeSpan(PcrValue);
        public ulong OPcrValue { get; init; }
        public TimeSpan OPcrTime => TsHelpers.GetPcrTimeSpan(OPcrValue);
    }
}
