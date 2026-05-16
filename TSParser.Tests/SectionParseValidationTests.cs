using NUnit.Framework;
using TSParser.Descriptors;
using TSParser.Descriptors.Dvb;
using TSParser.Service;
using TSParser.Tables.DvbTables;

namespace TSParser.Tests;

[TestFixture]
public class SectionParseValidationTests
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
}
