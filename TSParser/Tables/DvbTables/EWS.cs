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

using System.Buffers.Binary;
using TSParser.Descriptors;
using TSParser.Service;

namespace TSParser.Tables.DvbTables;

public record EWS : Table
{
    public override ushort TablePid { get; }
    public ushort EwsRegionId { get; }
    public bool PrivateIndicator { get; }
    public ushort RegionDescriptorLength { get; }
    public ushort ZoneLoopLength { get; }
    public List<Descriptor> EwsDescriptorList { get; } = default!;
    public List<ZoneLoop> ZoneLoopList { get; } = default!;
    public EWS(ReadOnlySpan<byte> bytes, ushort ewsPid) : base(bytes)
    {
        TablePid = ewsPid;
        TableId = bytes[0];
        SectionSyntaxIndicator = (bytes[1] & 0x80) != 0;
        PrivateIndicator = (bytes[1] & 0x40) != 0;
        //reserved 6 bits
        SectionLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2)) & 0x0FFF);
        EwsRegionId = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
        //reserved 2 bits
        VersionNumber = (byte)((bytes[5] & 0x3E) >> 1);
        CurrentNextIndicator = (bytes[5] & 0x01) != 0;
        SectionNumber = bytes[6];
        LastSectionNumber = bytes[7];
        //reserved 4 bits
        RegionDescriptorLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(8, 2)) & 0x0FFF);
        var pointer = 10;
        var descAllocation = $"Table: EWS, Region id: {EwsRegionId}, Section number: {SectionNumber}";

        if (RegionDescriptorLength > 0)
        {
            EwsDescriptorList = DescriptorFactory.GetDescriptorList(bytes[pointer..(pointer + RegionDescriptorLength)], descAllocation);
        }
        pointer += RegionDescriptorLength;
        //reserved 4 bits
        ZoneLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(pointer, 2)) & 0x0FFF);
        pointer += 2;
        if (ZoneLoopLength > 0)
        {
            ZoneLoopList = GetZoneLoopList(bytes[pointer..^4]);
        }

        CRC32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
    }

    public virtual bool Equals(EWS? table)
    {
        if (table == null) return false;
        return CRC32 == table.CRC32;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (int)CRC32;
        }
    }

    private List<ZoneLoop> GetZoneLoopList(ReadOnlySpan<byte> bytes)
    {
        var pointer = 0;
        List<ZoneLoop> zoneLoops = new();
        while (pointer < bytes.Length)
        {
            ZoneLoop zl = new(bytes[pointer..]);
            pointer += zl.ZoneDescriptorLength + 5;
            zoneLoops.Add(zl);
        }
        return zoneLoops;
    }

    public override string Print(int prefixLen)
    {
        string headerPrefix = Utils.HeaderPrefix(prefixLen);
        string prefix = Utils.Prefix(prefixLen);
        var ews = $"{headerPrefix}-=EWS=-\n";
        ews += $"{prefix}EWS region id: {EwsRegionId}\n";

        if (RegionDescriptorLength > 0)
        {
            ews += $"{prefix}EWS descriptor list count: {EwsDescriptorList.Count}\n";
            foreach (var desc in EwsDescriptorList)
            {
                ews += desc.Print(prefixLen + 4);
            }
        }

        if (ZoneLoopLength > 0)
        {
            ews += $"{prefix}Zone loop list count: {ZoneLoopList.Count}\n";
            foreach (var zl in ZoneLoopList)
            {
                ews += zl.Print(prefixLen + 4);
            }
        }

        return ews;
    }

}

public struct ZoneLoop
{
    public ushort EwsZoneId { get; }
    public bool EwsState { get; }
    public ushort ZoneDescriptorLength { get; }
    public List<Descriptor> ZoneDesciptorList { get; }

    public ZoneLoop(ReadOnlySpan<byte> bytes)
    {
        var pointer = 0;
        EwsZoneId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
        pointer += 2;
        //reserved 7 bits
        EwsState = (bytes[pointer + 2] & 0x01) != 0;
        pointer++;
        //reserved 4 bits
        ZoneDescriptorLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
        pointer += 2;
        var descAllocation = $"Table: EWS, Zone id: {EwsZoneId}";
        ZoneDesciptorList = DescriptorFactory.GetDescriptorList(bytes.Slice(pointer, ZoneDescriptorLength), descAllocation);
    }

    public string Print(int prefixLen)
    {
        string headerPrefix = Utils.HeaderPrefix(prefixLen);
        string prefix = Utils.Prefix(prefixLen);
        string zl = $"{headerPrefix}Zone loop\n";
        zl += $"{prefix}EWS zone id: {EwsZoneId}\n";
        zl += $"{prefix}EWS state: {EwsState}\n";
        zl += $"{prefix}Zone descriptor length: {ZoneDescriptorLength}\n";
        if (ZoneDesciptorList?.Count > 0)
        {
            zl += $"{prefix}Zone descriptor list count: {ZoneDesciptorList.Count}\n";
            foreach (var desc in ZoneDesciptorList)
            {
                zl += desc.Print(prefixLen + 4);
            }
        }
        return zl;
    }
}
