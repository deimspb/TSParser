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
using System.Text;
using TSParser.Enums;
using TSParser.Service;

namespace TSParser.Descriptors.Scte35Descriptors
{
    public record SegmentationDescriptor_0x02 : Scte35Descriptor
    {
        public bool ProgramSegmentationFlag { get; }
        public bool SegmentationDurationFlag { get; }
        public bool DeliveryNotRestictedFlag { get; }
        public byte SegmentationUpidType { get; }
        public byte SegmentationUpidLength { get; }


        public DeliveryRestrictions DeliveryRestrictions { get; }
        public SegmentationUpid[] SegmentationUpids { get; }
        public SegmentationComponent[] Components { get; }
        public uint SegmentationEventId { get; }
        public bool SegmentationEventCancelIndicator { get; }
        public ulong SegmentationDuration { get; }
        public byte SegmentationTypeId { get; }
        public byte SegmentNum { get; }
        public byte SegmentsExpected { get; }
        public byte SubSegmentNum { get; }
        public byte SubSegmentsExpected { get; }
        public SegmentationDescriptor_0x02(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 6;
            SegmentationEventId = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]);
            pointer += 4;
            SegmentationEventCancelIndicator = (bytes[pointer++] & 0x80) != 0;
            //reserved 7 bits
            if (!SegmentationEventCancelIndicator)
            {
                ProgramSegmentationFlag = (bytes[pointer] & 0x80) != 0;
                SegmentationDurationFlag = (bytes[pointer] & 0x40) != 0;
                DeliveryNotRestictedFlag = (bytes[pointer] & 0x20) != 0;
                if (!DeliveryNotRestictedFlag)
                {
                    DeliveryRestrictions = new DeliveryRestrictions(bytes[pointer++..]);
                }
                else
                {
                    //reserved 5  bits
                    pointer++;
                }
                if (!ProgramSegmentationFlag)
                {
                    var componentCount = bytes[pointer++];
                    Components = new SegmentationComponent[componentCount];
                    for (int i = 0; i < componentCount; i++)
                    {
                        Components[i] = new SegmentationComponent(bytes[pointer..]);
                        pointer += 6;
                    }
                }
                if (SegmentationDurationFlag)
                {
                    SegmentationDuration = BinaryPrimitives.ReadUInt64BigEndian(bytes[pointer..]) >> 24;
                    pointer += 5;
                }
                //TODO segmentation upid type name
                SegmentationUpidType = bytes[pointer++];
                SegmentationUpidLength = bytes[pointer++];
                // segmnentation upid
                if (SegmentationUpidType == (byte)SegmentationUpidTypeEnum.SegmentationUPIDTypeMID)
                {
                    List<SegmentationUpid> segmentationUpidTypes = new List<SegmentationUpid>();

                    var innerPointer = pointer;
                    while (innerPointer < SegmentationUpidLength)
                    {
                        var upidType = bytes[innerPointer++];
                        var upidLength = bytes[innerPointer++];
                        var upidValue = bytes.Slice(innerPointer, upidLength);
                        segmentationUpidTypes.Add(new SegmentationUpid(upidValue, upidType));
                    }

                    SegmentationUpids = segmentationUpidTypes.ToArray();
                }
                else
                {
                    SegmentationUpids = new SegmentationUpid[] { new SegmentationUpid(bytes.Slice(pointer, SegmentationUpidLength), SegmentationUpidType) };
                }
                pointer += SegmentationUpidLength;
                SegmentationTypeId = bytes[pointer++];
                //TODO: segmentation type ID name
                SegmentNum = bytes[pointer++];
                SegmentsExpected = bytes[pointer++];

                if (SegmentationTypeId == 0x34 ||
                    SegmentationTypeId == 0x36 ||
                    SegmentationTypeId == 0x38 ||
                    SegmentationTypeId == 0x3A)
                {
                    if (pointer < bytes.Length)
                    {
                        SubSegmentNum = bytes[pointer++];
                        SubSegmentsExpected = bytes[pointer++];
                    }
                }
            }
        }

        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Segmentation descriptor\n";

            str += $"{prefix}Segmentation event id: {SegmentationEventId}\n";
            str += $"{prefix}Segmentation Event Cancel Indicator: {SegmentationEventCancelIndicator}\n";

            if (!SegmentationEventCancelIndicator)
            {
                str += $"{prefix}Program Segmentation Flag: {ProgramSegmentationFlag}\n";
                str += $"{prefix}Segmentation Duration Flag: {SegmentationDurationFlag}\n";
                str += $"{prefix}Delivery Not Resticted Flag: {DeliveryNotRestictedFlag}\n";
                if (!DeliveryNotRestictedFlag)
                {
                    str += DeliveryRestrictions.Print(prefixLen + 4);
                }
                if (!ProgramSegmentationFlag)
                {
                    foreach (var item in Components)
                    {
                        str += item.Print(prefixLen + 4);
                    }
                }
                if (SegmentationDurationFlag)
                {
                    str += $"{prefix}Segmentation duration: {Utils.GetPtsDtsValue(SegmentationDuration)}\n";
                }
                str += $"{prefix}Segmentation Upid Type: {SegmentationUpidType}\n";
                str += $"{prefix}Segmentation Upid Length: {SegmentationUpidLength}\n";
                foreach (var item in SegmentationUpids)
                {
                    str += item.Print(prefixLen + 4);
                }
                str += $"{prefix}Segmentation Type Id: {SegmentationTypeId}, type name: {GetSegmentationTypeIdName((SegmentationTypeIdEnum)SegmentationTypeId)}\n";
                str += $"{prefix}Segment Num: {SegmentNum}\n";
                str += $"{prefix}Segments Expected: {SegmentsExpected}\n";
                if (SegmentationTypeId == 0x34 ||
                    SegmentationTypeId == 0x36 ||
                    SegmentationTypeId == 0x38 ||
                    SegmentationTypeId == 0x3A)
                {
                    str += $"{prefix}Sub Segment Num: {SubSegmentNum}\n";
                    str += $"{prefix}Sub Segments Expected: {SubSegmentsExpected}\n";

                }

            }

            return str;
        }

        private string GetSegmentationTypeIdName(SegmentationTypeIdEnum typeId)
        {
            switch (typeId)
            {
                case SegmentationTypeIdEnum.SegmentationTypeNotIndicated: return "Not Indicated";
                case SegmentationTypeIdEnum.SegmentationTypeContentIdentification: return "Content Identification";
                case SegmentationTypeIdEnum.SegmentationTypeProgramStart: return "Program Start";
                case SegmentationTypeIdEnum.SegmentationTypeProgramEnd: return "Program End";
                case SegmentationTypeIdEnum.SegmentationTypeProgramEarlyTermination: return "Program Early Termination";
                case SegmentationTypeIdEnum.SegmentationTypeProgramBreakaway: return "Program Breakaway";
                case SegmentationTypeIdEnum.SegmentationTypeProgramResumption: return "Program Resumption";
                case SegmentationTypeIdEnum.SegmentationTypeProgramRunoverPlanned: return "Program Runover Planned";
                case SegmentationTypeIdEnum.SegmentationTypeProgramRunoverUnplanned: return "Program Runover Unplanned";
                case SegmentationTypeIdEnum.SegmentationTypeProgramOverlapStart: return "Program Overlap Start";
                case SegmentationTypeIdEnum.SegmentationTypeProgramBlackoutOverride: return "Program Blackout Override";
                case SegmentationTypeIdEnum.SegmentationTypeProgramStartInProgress: return "Program Start - In Progress";
                case SegmentationTypeIdEnum.SegmentationTypeChapterStart: return "Chapter Start";
                case SegmentationTypeIdEnum.SegmentationTypeChapterEnd: return "Chapter End";
                case SegmentationTypeIdEnum.SegmentationTypeBreakStart: return "Break Start";
                case SegmentationTypeIdEnum.SegmentationTypeBreakEnd: return "Break End";
                case SegmentationTypeIdEnum.SegmentationTypeOpeningCreditStart: return "Opening Credit Start";
                case SegmentationTypeIdEnum.SegmentationTypeOpeningCreditEnd: return "Opening Credit End";
                case SegmentationTypeIdEnum.SegmentationTypeClosingCreditStart: return "Closing Credit Start";
                case SegmentationTypeIdEnum.SegmentationTypeClosingCreditEnd: return "Closing Credit End";
                case SegmentationTypeIdEnum.SegmentationTypeProviderAdStart: return "Provider Advertisement Start";
                case SegmentationTypeIdEnum.SegmentationTypeProviderAdEnd: return "Provider Advertisement End";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorAdStart: return "Distributor Advertisement Start";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorAdEnd: return "Distributor Advertisement End";
                case SegmentationTypeIdEnum.SegmentationTypeProviderPOStart: return "Provider Placement Opportunity Start";
                case SegmentationTypeIdEnum.SegmentationTypeProviderPOEnd: return "Provider Placement Opportunity End";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorPOStart: return "Distributor Placement Opportunity Start";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorPOEnd: return "Distributor Placement Opportunity End";
                case SegmentationTypeIdEnum.SegmentationTypeProviderOverlayPOStart: return "Provider Overlay Placement Opportunity Start";
                case SegmentationTypeIdEnum.SegmentationTypeProviderOverlayPOEnd: return "Provider Overlay Placement Opportunity End";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorOverlayPOStart: return "Distributor Overlay Placement Opportunity Start";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorOverlayPOEnd: return "Distributor Overlay Placement Opportunity End";
                case SegmentationTypeIdEnum.SegmentationTypeProviderPromoStart: return "Provider Promo Start";
                case SegmentationTypeIdEnum.SegmentationTypeProviderPromoEnd: return "Provider Promo End";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorPromoStart: return "Distributor Promo Start";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorPromoEnd: return "Distributor Promo End";
                case SegmentationTypeIdEnum.SegmentationTypeUnscheduledEventStart: return "Unscheduled Event Start";
                case SegmentationTypeIdEnum.SegmentationTypeUnscheduledEventEnd: return "Unscheduled Event End";
                case SegmentationTypeIdEnum.SegmentationTypeAltConOppStart: return "Alternate Content Opportunity Start";
                case SegmentationTypeIdEnum.SegmentationTypeAltConOppEnd: return "Alternate Content Opportunity End";
                case SegmentationTypeIdEnum.SegmentationTypeProviderAdBlockStart: return "Provider Ad Block Start";
                case SegmentationTypeIdEnum.SegmentationTypeProviderAdBlockEnd: return "Provider Ad Block End";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorAdBlockStart: return "Distributor Ad Block Start";
                case SegmentationTypeIdEnum.SegmentationTypeDistributorAdBlockEnd: return "Distributor Ad Block End";
                case SegmentationTypeIdEnum.SegmentationTypeNetworkStart: return "Network Start";
                case SegmentationTypeIdEnum.SegmentationTypeNetworkEnd: return "Network End";
                default: return "Unknown";
            }
        }
    }
    public struct DeliveryRestrictions
    {
        public bool WebDeliveryAllowedFlag { get; }
        public bool NoRegionalBlackoutFlag { get; }
        public bool ArchiveAllowedFlag { get; }
        public byte DeviceRestrictions { get; }
        public DeliveryRestrictions(ReadOnlySpan<byte> bytes)
        {
            WebDeliveryAllowedFlag = (bytes[0] & 0x10) != 0;
            NoRegionalBlackoutFlag = (bytes[0] & 0x8) != 0;
            ArchiveAllowedFlag = (bytes[0] & 0x4) != 0;
            DeviceRestrictions = (byte)(bytes[0] & 0x3);
        }

        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Delivery restrictions\n";

            str += $"{prefix}Web Delivery Allowed Flag: {WebDeliveryAllowedFlag}\n";
            str += $"{prefix}No Regional Blackout Flag: {NoRegionalBlackoutFlag}\n";
            str += $"{prefix}Archive Allowed Flag: {ArchiveAllowedFlag}\n";
            str += $"{prefix}Device Restrictions: {DeviceRestrictions}\n";

            return str;
        }
    }
    public record SegmentationUpid
    {
        public byte segmentationUpidType { get; }
        public uint FormatIdentifier { get; }
        public string SegmentationUpidFormat { get; }
        public string Value { get; }
        public SegmentationUpid(ReadOnlySpan<byte> bytes, byte UpidType)
        {
            switch (UpidType)
            {
                case (byte)SegmentationUpidTypeEnum.SegmentationUPIDTypeEIDR:
                    {
                        segmentationUpidType = UpidType;
                        SegmentationUpidFormat = "text";
                        Value = canonicalEIDR(bytes);
                    }
                    break;
                case (byte)SegmentationUpidTypeEnum.SegmentationUPIDTypeISAN:
                case (byte)SegmentationUpidTypeEnum.SegmentationUPIDTypeISANDeprecated:
                    {
                        segmentationUpidType = UpidType;
                        SegmentationUpidFormat = "base-64";
                        Value = Convert.ToBase64String(bytes);
                    }
                    break;
                case (byte)SegmentationUpidTypeEnum.SegmentationUPIDTypeMPU:
                    {
                        segmentationUpidType = UpidType;
                        SegmentationUpidFormat = "base-64";
                        FormatIdentifier = BinaryPrimitives.ReadUInt16BigEndian(bytes);
                        Value = Convert.ToBase64String(bytes[4..]);
                    }
                    break;
                case (byte)SegmentationUpidTypeEnum.SegmentationUPIDTypeTI:
                    {
                        segmentationUpidType = UpidType;
                        SegmentationUpidFormat = "text";
                        Value = BinaryPrimitives.ReadInt64BigEndian(bytes).ToString();
                    }
                    break;
                default:
                    {
                        segmentationUpidType = UpidType;
                        SegmentationUpidFormat = "text";
                        Value = Encoding.UTF8.GetString(bytes);
                    }
                    break;
            }
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Segmentation upid type\n";

            str += $"{prefix}Segmentation upid type: {segmentationUpidType} Type name: {GetUpidTypeName((SegmentationUpidTypeEnum)segmentationUpidType,Value)}\n";
            str += $"{prefix}Format: {SegmentationUpidFormat}\n";
            str += $"{prefix}Format identifier: {FormatIdentifier}\n";
            str += $"{prefix}Value: {Value}\n";

            return str;
        }

        private string GetUpidTypeName(SegmentationUpidTypeEnum upidType, string value)
        {
            switch (upidType)
            {
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeNotUsed: return "Not Used";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeUserDefined: return "User Defined";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeISCI: return "ISCI";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeAdID: return "Ad-ID";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeUMID: return "UMID";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeISANDeprecated: return "ISAN (Deprecated)";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeISAN: return "ISAN";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeTID: return "TID";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeTI: return "TI";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeADI: return "ADI";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeEIDR: return $"EIDR: {EidrTypeName(value)}";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeATSC: return "ATSC Content Identifier";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeMPU: return "MPU()";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeMID: return "MID()";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeADS: return "ADS Information";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeURI: return "URI";
                case SegmentationUpidTypeEnum.SegmentationUPIDTypeUUID: return "UUID";
                default: return "Unknown";
            }
        }

        private string EidrTypeName(string value)
        {
            if (value.Contains("10.5237"))
                return "Party ID";
            if (value.Contains("10.5238"))
                return "User ID";
            if (value.Contains("10.5239"))
                return "Service ID";
            if (value.Contains("10.5240"))
                return "Content ID";
            return "";
        }

        private static string canonicalEIDR(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IndexOf((byte)0x2F) > 0)
                return Encoding.UTF8.GetString(bytes);

            //invalid len
            if (bytes.Length != 12)
                return "";
            //TODO:
            return $"10.to do to do";
        }
    }
    public struct SegmentationComponent
    {
        public byte ComponentTag { get; }
        public ulong PtsOffset { get; }
        public SegmentationComponent(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            ComponentTag = bytes[pointer++];
            PtsOffset = (ulong)(bytes[pointer] & 0x01) << 32
                    | (ulong)(bytes[pointer++]) << 24
                    | (ulong)(bytes[pointer++]) << 16
                    | (ulong)(bytes[pointer++]) << 8
                    | bytes[pointer++];
        }

        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Segmentation component\n";
            str += $"{prefix}Tag: {ComponentTag}\n";
            str += $"{prefix}Pts offset: {Utils.GetPtsDtsValue(PtsOffset)}\n";

            return str;
        }
    }
}
