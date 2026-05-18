using TSParser.Service;
using TSParser.TransportStream;

namespace TSParser.Tables;

internal sealed class PsiSectionAssembler
{
    private readonly ushort pid;
    private readonly byte[] headerBuffer = new byte[3];
    private byte[] currentSection = null!;
    private int expectedSectionBytes;
    private int headerBytes;
    private int sectionBytes;
    private byte? lastContinuityCounter;

    internal PsiSectionAssembler(ushort pid)
    {
        this.pid = pid;
    }

    internal IReadOnlyList<ReadOnlyMemory<byte>> PushPacket(TsPacket packet)
    {
        if (packet.Pid != pid)
        {
            Logger.Send(LogStatus.WARNING, $"PsiSectionAssembler PID mismatch: expected 0x{pid:X4}, got 0x{packet.Pid:X4}");
            return Array.Empty<ReadOnlyMemory<byte>>();
        }

        if (!packet.HasPayload || packet.Payload.Length == 0)
        {
            return Array.Empty<ReadOnlyMemory<byte>>();
        }

        var readySections = new List<ReadOnlyMemory<byte>>(2);
        HandleContinuity(packet);

        var payload = packet.Payload.AsSpan();
        var offset = 0;

        if (packet.PayloadUnitStartIndicator)
        {
            var pointerField = payload[0];
            if (pointerField > payload.Length - 1)
            {
                Logger.Send(LogStatus.WARNING, $"Invalid pointer_field {pointerField} for pid: {packet.Pid}");
                ResetCurrentSection();
                return Array.Empty<ReadOnlyMemory<byte>>();
            }

            offset = 1;
            var prefixLength = Math.Min(pointerField, payload.Length - offset);
            if (prefixLength > 0)
            {
                AppendPayload(payload.Slice(offset, prefixLength), readySections, packet.Pid);
                offset += prefixLength;
            }
        }

        if (offset >= payload.Length)
        {
            return readySections;
        }

        if (!HasPendingSectionState() && !packet.PayloadUnitStartIndicator)
        {
            return readySections;
        }

        AppendPayload(payload[offset..], readySections, packet.Pid);
        return readySections;
    }

    internal void Reset()
    {
        ResetCurrentSection();
        lastContinuityCounter = null;
    }

    private void HandleContinuity(TsPacket packet)
    {
        if (!packet.HasPayload)
        {
            return;
        }

        var hasDiscontinuityFlag = packet.HasAdaptationField && packet.Adaptation_field.DiscontinuityIndicator;
        if (!hasDiscontinuityFlag && lastContinuityCounter.HasValue)
        {
            var expectedCc = (lastContinuityCounter.Value + 1) & 0x0F;
            if (packet.ContinuityCounter != expectedCc)
            {
                if (HasPendingSectionState())
                {
                    Logger.Send(
                        LogStatus.WARNING,
                        $"Continuity mismatch for pid 0x{packet.Pid:X4}: expected {expectedCc}, got {packet.ContinuityCounter}. Pending PSI section dropped.");
                    ResetCurrentSection();
                }
            }
        }

        lastContinuityCounter = packet.ContinuityCounter;
    }

    private void AppendPayload(ReadOnlySpan<byte> source, List<ReadOnlyMemory<byte>> readySections, ushort packetPid)
    {
        var offset = 0;
        while (offset < source.Length)
        {
            if (!HasPendingSectionState() && source[offset] == 0xFF)
            {
                return;
            }

            if (headerBytes < 3)
            {
                var headerBytesToCopy = Math.Min(3 - headerBytes, source.Length - offset);
                source.Slice(offset, headerBytesToCopy).CopyTo(headerBuffer.AsSpan(headerBytes));
                headerBytes += headerBytesToCopy;
                offset += headerBytesToCopy;
                if (headerBytes < 3)
                {
                    return;
                }

                var sectionLength = ((headerBuffer[1] & 0x0F) << 8) + headerBuffer[2];
                if (sectionLength <= 0 || sectionLength > SectionParseValidation.MaxSectionLength)
                {
                    Logger.Send(LogStatus.WARNING, $"Invalid section length {sectionLength} for pid: {packetPid}");
                    ResetCurrentSection();
                    return;
                }

                expectedSectionBytes = sectionLength + 3;
                currentSection = new byte[expectedSectionBytes];
                Buffer.BlockCopy(headerBuffer, 0, currentSection, 0, 3);
                sectionBytes = 3;
            }

            var bodyBytesLeft = expectedSectionBytes - sectionBytes;
            var payloadBytesLeft = source.Length - offset;
            var bytesToCopy = Math.Min(bodyBytesLeft, payloadBytesLeft);
            source.Slice(offset, bytesToCopy).CopyTo(currentSection.AsSpan(sectionBytes));

            sectionBytes += bytesToCopy;
            offset += bytesToCopy;

            if (sectionBytes == expectedSectionBytes)
            {
                readySections.Add(currentSection);
                ResetCurrentSection();
            }
        }
    }

    private bool HasPendingSectionState() =>
        headerBytes > 0 || sectionBytes > 0 || expectedSectionBytes > 0;

    private void ResetCurrentSection()
    {
        headerBytes = 0;
        sectionBytes = 0;
        expectedSectionBytes = 0;
        currentSection = null!;
    }
}
