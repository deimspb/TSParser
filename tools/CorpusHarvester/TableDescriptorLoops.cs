using System.Buffers.Binary;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
namespace CorpusHarvester;

internal static class TableDescriptorLoops
{
    public static void Visit(Table table, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        switch (table)
        {
            case CAT cat: VisitCat(cat, onLoop); break;
            case PMT pmt: VisitPmt(pmt, onLoop); break;
            case NIT nit: VisitNit(nit, onLoop); break;
            case SDT sdt: VisitSdt(sdt, onLoop); break;
            case BAT bat: VisitBat(bat, onLoop); break;
            case EIT eit: VisitEit(eit, onLoop); break;
            case TOT tot: VisitTot(tot, onLoop); break;
            case AIT ait: VisitAit(ait, onLoop); break;
            case SCTE35 scte: VisitScte35(scte, onLoop); break;
            case EWS ews: VisitEws(ews, onLoop); break;
            case EEWS eews: VisitEews(eews, onLoop); break;
        }
    }

    private static void VisitCat(CAT cat, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = cat.TableBytes;
        if (bytes.Length <= 12)
        {
            return;
        }

        onLoop(bytes[8..^4], null);
    }

    private static void VisitPmt(PMT pmt, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = pmt.TableBytes;
        var pointer = 12;
        if (pmt.ProgramInfoLength > 0)
        {
            onLoop(bytes.Slice(pointer, pmt.ProgramInfoLength), null);
            pointer += pmt.ProgramInfoLength;
        }

        var esArea = bytes[pointer..^4];
        var esPointer = 0;
        while (esPointer < esArea.Length)
        {
            if (esArea.Length - esPointer < 5)
            {
                return;
            }

            esPointer++;
            esPointer += 2;
            var esInfoLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(esArea[esPointer..]) & 0x0FFF);
            esPointer += 2;
            if (esInfoLength > 0)
            {
                onLoop(esArea.Slice(esPointer, esInfoLength), null);
            }

            esPointer += esInfoLength;
        }
    }

    private static void VisitNit(NIT nit, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = nit.TableBytes;
        var pointer = 10;
        if (nit.NetworkDescriptorsLenght > 0)
        {
            onLoop(bytes.Slice(pointer, nit.NetworkDescriptorsLenght), null);
            pointer += nit.NetworkDescriptorsLenght;
        }

        pointer += 2;
        var loopArea = bytes.Slice(pointer, nit.TransportStreamLoopLenght);
        var itemPointer = 0;
        while (itemPointer < loopArea.Length)
        {
            var descLen = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(loopArea[(itemPointer + 4)..]) & 0x0FFF);
            if (descLen > 0)
            {
                onLoop(loopArea.Slice(itemPointer + 6, descLen), null);
            }

            itemPointer += descLen + 6;
        }
    }

    private static void VisitSdt(SDT sdt, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = sdt.TableBytes;
        var items = bytes[11..^4];
        var pointer = 0;
        while (pointer < items.Length)
        {
            var descLen = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(items[(pointer + 3)..]) & 0x0FFF);
            if (descLen > 0)
            {
                onLoop(items.Slice(pointer + 5, descLen), null);
            }

            pointer += descLen + 5;
        }
    }

    private static void VisitBat(BAT bat, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = bat.TableBytes;
        var pointer = 10;
        if (bat.BouquetDescriptorsLenght > 0)
        {
            onLoop(bytes.Slice(pointer, bat.BouquetDescriptorsLenght), null);
            pointer += bat.BouquetDescriptorsLenght;
        }

        pointer += 2;
        var loopArea = bytes.Slice(pointer, bat.TransportStreamLoopLenght);
        var itemPointer = 0;
        while (itemPointer < loopArea.Length)
        {
            var descLen = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(loopArea[(itemPointer + 4)..]) & 0x0FFF);
            if (descLen > 0)
            {
                onLoop(loopArea.Slice(itemPointer + 6, descLen), null);
            }

            itemPointer += descLen + 6;
        }
    }

    private static void VisitEit(EIT eit, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = eit.TableBytes;
        var events = bytes[14..^4];
        var pointer = 0;
        while (pointer < events.Length)
        {
            var descLen = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(events[(pointer + 10)..]) & 0x0FFF);
            if (descLen > 0)
            {
                onLoop(events.Slice(pointer + 12, descLen), null);
            }

            pointer += descLen + 12;
        }
    }

    private static void VisitTot(TOT tot, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        if (tot.DescriptorLoopLength == 0)
        {
            return;
        }

        onLoop(tot.TableBytes.Slice(10, tot.DescriptorLoopLength), null);
    }

    private static void VisitAit(AIT ait, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = ait.TableBytes;
        const byte caller = 0x74;
        var pointer = 10;
        if (ait.CommonDescriptorsLength > 0)
        {
            onLoop(bytes.Slice(pointer, ait.CommonDescriptorsLength), caller);
            pointer += ait.CommonDescriptorsLength;
        }

        pointer += 2;
        var appArea = bytes.Slice(pointer, ait.ApplicationLoopLength);
        var appPointer = 0;
        while (appPointer < appArea.Length)
        {
            var descLen = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(appArea[(appPointer + 7)..]) & 0x0FFF);
            if (descLen > 0)
            {
                onLoop(appArea.Slice(appPointer + 9, descLen), caller);
            }

            appPointer += descLen + 9;
        }
    }

    private static void VisitScte35(SCTE35 scte, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        if (scte.DescriptorLoopLength == 0)
        {
            return;
        }

        var bytes = scte.TableBytes;
        var pointer = 0;
        pointer++;
        pointer += 2;
        pointer++;
        pointer += 6;
        pointer++;
        pointer += 2;
        pointer++;
        if (scte.SpliceCommandLength == 0xFFF)
        {
            pointer += scte.SpliceCommandItem.SpliceCommandLength;
        }
        else
        {
            pointer += scte.SpliceCommandLength;
        }

        pointer += 2;
        onLoop(bytes.Slice(pointer, scte.DescriptorLoopLength), 0xFC);
    }

    private static void VisitEws(EWS ews, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = ews.TableBytes;
        var pointer = 10;
        if (ews.RegionDescriptorLength > 0)
        {
            onLoop(bytes.Slice(pointer, ews.RegionDescriptorLength), null);
            pointer += ews.RegionDescriptorLength;
        }

        pointer += 2;
        var zoneArea = bytes[pointer..^4];
        var zonePointer = 0;
        while (zonePointer < zoneArea.Length)
        {
            var descLen = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(zoneArea[(zonePointer + 3)..]) & 0x0FFF);
            if (descLen > 0)
            {
                onLoop(zoneArea.Slice(zonePointer + 5, descLen), null);
            }

            zonePointer += descLen + 5;
        }
    }

    private static void VisitEews(EEWS eews, Action<ReadOnlySpan<byte>, byte?> onLoop)
    {
        var bytes = eews.TableBytes;
        var pointer = 10;
        if (eews.EewsDescriptorLength > 0)
        {
            onLoop(bytes.Slice(pointer, eews.EewsDescriptorLength), null);
            pointer += eews.EewsDescriptorLength;
        }

        pointer += 2;
        var deviceArea = bytes[pointer..^4];
        var devicePointer = 0;
        while (devicePointer < deviceArea.Length)
        {
            var descLen = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(deviceArea[(devicePointer + 3)..]) & 0x0FFF);
            if (descLen > 0)
            {
                onLoop(deviceArea.Slice(devicePointer + 5, descLen), null);
            }

            devicePointer += descLen + 5;
        }
    }
}
