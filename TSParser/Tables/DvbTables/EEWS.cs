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

public record EEWS : Table
{
    public override ushort TablePid { get; }
    public ushort EewsGroupId { get; }
    public bool PrivateIndicator { get; }
    public ushort EewsDescriptorLength { get; }
    public List<Descriptor> EewsDescriptorList { get; } = default!;
    public List<DeviceLoop> DeviceLoopList { get; } = default!;
    public ushort EewsDeviceLoopLength { get; }

    public EEWS(ReadOnlySpan<byte> bytes, ushort eewsPid) : base(bytes)
    {
        TablePid = eewsPid;
        TableId = bytes[0];
        SectionSyntaxIndicator = (bytes[1] & 0x80) != 0;
        PrivateIndicator = (bytes[1] & 0x40) != 0;
        //reserved 2 bits
        SectionLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2)) & 0x0FFF);
        EewsGroupId = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
        //reserved 2 bits
        VersionNumber = (byte)((bytes[5] & 0x3E) >> 1);
        CurrentNextIndicator = (bytes[5] & 0x01) != 0;
        SectionNumber = bytes[6];
        LastSectionNumber = bytes[7];
        //reserved 4 bits
        EewsDescriptorLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(8, 2)) & 0x0FFF);
        var pointer = 10;
        var descAllocation = $"Table: EEWS, Group id: {EewsGroupId}, Section number: {SectionNumber}";
        if (EewsDescriptorLength > 0)
        {
            EewsDescriptorList = DescriptorFactory.GetDescriptorList(bytes[pointer..(pointer + EewsDescriptorLength)], descAllocation);
        }
        pointer += EewsDescriptorLength;
        //reserved 4 bits
        EewsDeviceLoopLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(pointer, 2)) & 0x0FFF);
        pointer += 2;
        if (EewsDeviceLoopLength > 0)
        {
            DeviceLoopList = GetDeviceLoopList(bytes[pointer..^4]);
        }
    }

    public List<DeviceLoop> GetDeviceLoopList(ReadOnlySpan<byte> bytes)
    {
        var deviceLoopList = new List<DeviceLoop>();
        var pointer = 0;
        while (pointer < bytes.Length)
        {
            var deviceLoop = new DeviceLoop(bytes[pointer..]);
            deviceLoopList.Add(deviceLoop);
            pointer += deviceLoop.EewsDeviceDescriptorLength + 5;
        }
        return deviceLoopList;
    }

    public virtual bool Equals(EEWS? table)
    {
        if (table is null) return false;
        return CRC32 == table.CRC32;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (int)CRC32;
        }
    }

    public override string Print(int prefixLen)
    {
        string prefix = Utils.Prefix(prefixLen);
        string headerPrefix = Utils.HeaderPrefix(prefixLen);
        string eews = $"{headerPrefix}EEWS table\n";
        eews += $"{prefix}Table id: 0x{TableId:X2}\n";
        eews += $"{prefix}EWS group id: {EewsGroupId}\n";
        eews += $"{prefix}Private indicator: {PrivateIndicator}\n";
        if (EewsDescriptorLength > 0)
        {
            eews += $"{prefix}EEWS descriptor list count: {EewsDescriptorList.Count}\n";
            foreach (var desc in EewsDescriptorList)
            {
                eews += desc.Print(prefixLen + 4);
            }
        }
        if (EewsDeviceLoopLength > 0)
        {
            eews += $"{prefix}EEWS device loop list count: {DeviceLoopList.Count}\n";
            foreach (var deviceLoop in DeviceLoopList)
            {
                eews += deviceLoop.Print(prefixLen + 4);
            }
        }
        return eews;
    }
}

public struct DeviceLoop
{
    public ushort EewsDeviceId { get; }
    public byte EewsType { get; }
    public byte EewsId { get; }
    public bool EewsState { get; }
    public ushort EewsDeviceDescriptorLength { get; }
    public List<Descriptor> EewsDeviceDescriptorList { get; }
    public DeviceLoop(ReadOnlySpan<byte> bytes)
    {
        var pointer = 0;
        EewsDeviceId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
        pointer += 2;
        EewsType = (byte)((bytes[pointer] & 0xC0) >> 6);
        EewsId = (byte)((bytes[pointer] & 0x3F) >> 1);
        EewsState = (bytes[pointer] & 0x01) != 0;
        pointer++;
        EewsDeviceDescriptorLength = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]) & 0x0FFF);
        pointer += 2;
        var descAllocation = $"Table: EEWS, Device id: {EewsDeviceId}, Type: {EewsType}, Id: {EewsId}";
        if (EewsDeviceDescriptorLength > 0)
        {
            EewsDeviceDescriptorList = DescriptorFactory.GetDescriptorList(bytes[pointer..(pointer + EewsDeviceDescriptorLength)], descAllocation);
        }
        else
        {
            EewsDeviceDescriptorList = new List<Descriptor>();
        }
    }

    public string Print(int prefixLen)
    {
        string headerPrefix = Utils.HeaderPrefix(prefixLen);
        string prefix = Utils.Prefix(prefixLen);
        string deviceLoop = $"{headerPrefix}Device loop\n";
        deviceLoop += $"{prefix}EWS device id: {EewsDeviceId}\n";
        deviceLoop += $"{prefix}EWS type: {EewsType}\n";
        deviceLoop += $"{prefix}EWS id: {EewsId}\n";
        deviceLoop += $"{prefix}EWS state: {EewsState}\n";
        if (EewsDeviceDescriptorLength > 0)
        {
            deviceLoop += $"{prefix}EWS device descriptor list count: {EewsDeviceDescriptorList.Count}\n";
            foreach (var desc in EewsDeviceDescriptorList)
            {
                deviceLoop += desc.Print(prefixLen + 4);
            }
        }
        return deviceLoop;
    }
}
