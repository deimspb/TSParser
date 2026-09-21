using NUnit.Framework;
using TSParser.Descriptors;
using TSParser.Descriptors.Dvb;
using TSParser.Service;
using TSParser.Tables.DvbTables;

namespace TSParser.Tests;

[TestFixture]
public sealed class SectionParseValidationTests
{
    [Test]
    public void Pat_rejects_section_length_larger_than_buffer()
    {
        var bytes = new byte[] { 0x00, 0xB0, 0x20, 0x00, 0x01, 0xC1, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        var ex = Assert.Throws<SectionParseException>(() => _ = new PAT(bytes));
        Assert.That(ex!.Reason, Is.EqualTo(ParseFailureReason.SectionLengthMismatch));
    }

    [Test]
    public void Descriptor_rejects_truncated_length_field()
    {
        var bytes = new byte[] { 0x48, 0x10, 0x01 };

        var ex = Assert.Throws<SectionParseException>(() => _ = new ServiceDescriptor_0x48(bytes));
        Assert.That(ex!.Reason, Is.EqualTo(ParseFailureReason.InvalidDescriptorLength));
    }

    [Test]
    public void GetDescriptorList_stops_when_declared_length_exceeds_buffer()
    {
        var bytes = new byte[] { 0x48, 0x10, 0x01 };

        var descriptors = DescriptorFactory.GetDescriptorList(bytes, "test");

        Assert.That(descriptors, Is.Empty);
    }

    [Test]
    public void GetDescriptorList_parses_zero_payload_descriptor()
    {
        var bytes = new byte[] { 0x48, 0x00 };

        var descriptors = DescriptorFactory.GetDescriptorList(bytes, "test");

        Assert.That(descriptors, Has.Count.EqualTo(1));
        Assert.That(descriptors[0].DescriptorTag, Is.EqualTo(0x48));
        Assert.That(descriptors[0].DescriptorLength, Is.Zero);
    }

    [Test]
    public void Pat_rejects_section_length_out_of_range()
    {
        var bytes = new byte[12];
        bytes[0] = 0x00;
        bytes[1] = 0xBF;
        bytes[2] = 0xFF; // section_length 4095

        var ex = Assert.Throws<SectionParseException>(() => _ = new PAT(bytes));
        Assert.That(ex!.Reason, Is.EqualTo(ParseFailureReason.SectionLengthOutOfRange));
    }

    [Test]
    public void Pat_rejects_misaligned_record_loop()
    {
        var bytes = new byte[]
        {
            0x00, 0xB0, 0x0A, 0x00, 0x01, 0xC1, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };

        var ex = Assert.Throws<SectionParseException>(() => _ = new PAT(bytes));
        Assert.That(ex!.Reason, Is.EqualTo(ParseFailureReason.InvalidRecordLoop));
    }

    [Test]
    public void GetDescriptorList_stops_after_first_descriptor_when_second_is_invalid()
    {
        var bytes = new byte[] { 0x48, 0x00, 0x48, 0x10, 0x01 };

        var descriptors = DescriptorFactory.GetDescriptorList(bytes, "test");

        Assert.That(descriptors, Has.Count.EqualTo(1));
        Assert.That(descriptors[0].DescriptorTag, Is.EqualTo(0x48));
    }

    [Test]
    public void Sdt_parses_service_with_empty_descriptor_loop()
    {
        var bytes = BuildSdtSection(descriptorLoopLength: 0);

        var sdt = new SDT(bytes);

        Assert.That(sdt.SdtItemsList, Has.Count.EqualTo(1));
        Assert.That(sdt.SdtItemsList[0].ServiceId, Is.EqualTo(1));
        Assert.That(sdt.SdtItemsList[0].DescriptorLoopLength, Is.Zero);
    }

    [Test]
    public void Sdt_stops_service_loop_when_descriptors_loop_length_exceeds_remaining_bytes()
    {
        var bytes = BuildSdtSection(descriptorLoopLength: 100);

        var sdt = new SDT(bytes);

        Assert.That(sdt.SdtItemsList, Is.Empty);
    }

    [Test]
    public void Mjd_utc_rejects_unrepresentable_calendar_value()
    {
        var bytes = new byte[] { 0x00, 0x00, 0x99, 0x99, 0x99 };

        Assert.That(Utils.TryGetDateTimeFromMJD_UTC(bytes, out _), Is.False);
        var ex = Assert.Throws<SectionParseException>(() => _ = Utils.GetDateTimeFromMJD_UTC(bytes));
        Assert.That(ex!.Reason, Is.EqualTo(ParseFailureReason.InvalidRecordLoop));
    }

    [Test]
    public void T2_delivery_descriptor_malformed_frequency_loop_does_not_hang()
    {
        var bytes = new byte[]
        {
            0x7F, 0x14, 0x04, 0x01, 0x00, 0x01,
            0x3C, 0x01, 0x00, 0x01, 0x00, 0xFC,
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00,
        };

        var parseTask = Task.Run(() => DescriptorFactory.GetDescriptor(bytes, "test"));

        Assert.That(parseTask.Wait(TimeSpan.FromSeconds(2)), Is.True, "Malformed T2 descriptor parse should finish promptly");
        Assert.DoesNotThrow(() => _ = parseTask.Result);
    }

    private static byte[] BuildSdtSection(ushort descriptorLoopLength)
    {
        var serviceLoopBytes = 5;
        var totalLength = 11 + serviceLoopBytes + 4;
        var bytes = new byte[totalLength];
        var sectionLength = totalLength - 3;

        bytes[0] = 0x42;
        bytes[1] = (byte)(0x80 | (sectionLength >> 8));
        bytes[2] = (byte)sectionLength;
        bytes[3] = 0x00;
        bytes[4] = 0x01;
        bytes[5] = 0x01;
        bytes[6] = 0x00;
        bytes[7] = 0x00;
        bytes[8] = 0x00;
        bytes[9] = 0x01;
        bytes[10] = 0xFF;
        bytes[11] = 0x00;
        bytes[12] = 0x01;
        bytes[13] = 0xFC;
        bytes[14] = (byte)(0x80 | ((descriptorLoopLength >> 8) & 0x0F));
        bytes[15] = (byte)descriptorLoopLength;
        return bytes;
    }
}
