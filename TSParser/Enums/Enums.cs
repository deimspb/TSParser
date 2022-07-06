// Copyright 2021 Eldar Nizamutdinov 
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

namespace TSParser.Enums
{
    public enum PesStreamId
    {
        Program_stream_map = 0b10111100,
        Private_stream_1 = 0b10111101,
        Padding_stream = 0b10111110,
        Private_stream_2 = 0b10111111,
        ECM_stream = 0b11110000,
        EMM_stream = 0b11110001,
        DSMCC_stream = 0b11110010,
        ISO_IEC_13522_stream = 0b11110011,
        H_222_1_type_A = 0b11110100,
        H_222_1_type_B = 0b11110101,
        H_222_1_type_C = 0b11110110,
        H_222_1_type_D = 0b11110111,
        H_222_1_type_E = 0b11111000,
        Ancillary_stream = 0b11111001,
        SL_packetizes_stream = 0b11111010,
        FlexMux_stream = 0b11111011,
        Program_stream_directory = 0b11111111
    }
    public enum ReservedPids
    {
        NullPacket = 0x1fff,
        PAT = 0x00,
        CAT = 0x01,
        NIT = 0x10,
        SDT = 0x11,
        EIT = 0x12,
        RST = 0x13,
        TDT = 0x14,
        NetworkSync = 0x15,
        RNT = 0x16,
        LLinbandSignalink = 0x1C,
        Measurement = 0x1D,
        DIT = 0x1E,
        SIT = 0x1F

    }
    public enum TsMode
    {
        DVB,
        ATSC,
        ISDB
    }
    public enum DecoderMode
    {
        SCTE35,
        Teletext,
        Sybtitling
    }
    public enum DecodeMode
    {
        Packet,
        Table
    }

    public enum SegmentationUpidTypeEnum
    {
        // SegmentationUPIDTypeNotUsed is the segmentation_upid_type for Not Used.
        SegmentationUPIDTypeNotUsed = 0x00,
        // SegmentationUPIDTypeUserDefined is the segmentation_upid_type for User
        // Defined.
        SegmentationUPIDTypeUserDefined = 0x01,
        // SegmentationUPIDTypeISCI is the segmentation_upid_type for ISCI
        SegmentationUPIDTypeISCI = 0x02,
        // SegmentationUPIDTypeAdID is the segmentation_upid_type for Ad-ID
        SegmentationUPIDTypeAdID = 0x03,
        // SegmentationUPIDTypeUMID is the segmentation_upid_type for UMID
        SegmentationUPIDTypeUMID = 0x04,
        // SegmentationUPIDTypeISANDeprecated is the segmentation_upid_type for
        // ISAN Deprecated.
        SegmentationUPIDTypeISANDeprecated = 0x05,
        // SegmentationUPIDTypeISAN is the segmentation_upid_type for ISAN.
        SegmentationUPIDTypeISAN = 0x06,
        // SegmentationUPIDTypeTID is the segmentation_upid_type for TID.
        SegmentationUPIDTypeTID = 0x07,
        // SegmentationUPIDTypeTI is the segmentation_upid_type for TI.
        SegmentationUPIDTypeTI = 0x08,
        // SegmentationUPIDTypeADI is the segmentation_upid_type for ADI.
        SegmentationUPIDTypeADI = 0x09,
        // SegmentationUPIDTypeEIDR is the segmentation_upid_type for EIDR.
        SegmentationUPIDTypeEIDR = 0x0a,
        // SegmentationUPIDTypeATSC is the segmentation_upid_type for ATSC Content
        // Identifier.
        SegmentationUPIDTypeATSC = 0x0b,
        // SegmentationUPIDTypeMPU is the segmentation_upid_type for MPU().
        SegmentationUPIDTypeMPU = 0x0c,
        // SegmentationUPIDTypeMID is the segmentation_upid_type for MID().
        SegmentationUPIDTypeMID = 0x0d,
        // SegmentationUPIDTypeADS is the segmentation_upid_type for ADS Information.
        SegmentationUPIDTypeADS = 0x0e,
        // SegmentationUPIDTypeURI is the segmentation_upid_type for URI.
        SegmentationUPIDTypeURI = 0x0f,
        // SegmentationUPIDTypeUUID is the segmentation_upid_type for UUID.
        SegmentationUPIDTypeUUID = 0x10,
    }

    public enum SegmentationTypeIdEnum
    {
        // SegmentationDescriptorTag is the splice_descriptor_tag for
        // segmentation_descriptor
        SegmentationDescriptorTag = 0x02,

        // SegmentationTypeNotIndicated is the segmentation_type_id for Not Indicated.
        SegmentationTypeNotIndicated = 0x00,
        // SegmentationTypeContentIdentification is the segmentation_type_id for
        // Content Identification.
        SegmentationTypeContentIdentification = 0x01,
        // SegmentationTypeProgramStart is the segmentation_type_id for Program Start.
        SegmentationTypeProgramStart = 0x10,
        // SegmentationTypeProgramEnd is the segmentation_type_id for Program End.
        SegmentationTypeProgramEnd = 0x11,
        // SegmentationTypeProgramEarlyTermination is the segmentation_type_id for
        // Program Early Termination.
        SegmentationTypeProgramEarlyTermination = 0x12,
        // SegmentationTypeProgramBreakaway is the segmentation_type_id for
        // Program Breakaway.
        SegmentationTypeProgramBreakaway = 0x13,
        // SegmentationTypeProgramResumption is the segmentation_type_id for Program
        // Resumption.
        SegmentationTypeProgramResumption = 0x14,
        // SegmentationTypeProgramRunoverPlanned is the segmentation_type_id for
        // Program Runover Planned.
        SegmentationTypeProgramRunoverPlanned = 0x15,
        // SegmentationTypeProgramRunoverUnplanned is the segmentation_type_id for
        // Program Runover Unplanned.
        SegmentationTypeProgramRunoverUnplanned = 0x16,
        // SegmentationTypeProgramOverlapStart is the segmentation_type_id for Program
        // Overlap Start.
        SegmentationTypeProgramOverlapStart = 0x17,
        // SegmentationTypeProgramBlackoutOverride is the segmentation_type_id for
        // Program Blackout Override.
        SegmentationTypeProgramBlackoutOverride = 0x18,
        // SegmentationTypeProgramStartInProgress is the segmentation_type_id for
        // Program Start - In Progress.
        SegmentationTypeProgramStartInProgress = 0x19,
        // SegmentationTypeChapterStart is the segmentation_type_id for Chapter Start.
        SegmentationTypeChapterStart = 0x20,
        // SegmentationTypeChapterEnd is the segmentation_type_id for Chapter End.
        SegmentationTypeChapterEnd = 0x21,
        // SegmentationTypeBreakStart is the segmentation_type_id for Break Start.
        // Added in ANSI/SCTE 2017.
        SegmentationTypeBreakStart = 0x22,
        // SegmentationTypeBreakEnd is the segmentation_type_id for Break End.
        // Added in ANSI/SCTE 2017.
        SegmentationTypeBreakEnd = 0x23,
        // SegmentationTypeOpeningCreditStart is the segmentation_type_id for
        // Opening Credit Start. Added in ANSI/SCTE 2020.
        SegmentationTypeOpeningCreditStart = 0x24,
        // SegmentationTypeOpeningCreditEnd is the segmentation_type_id for
        // Opening Credit End. Added in ANSI/SCTE 2020.
        SegmentationTypeOpeningCreditEnd = 0x25,
        // SegmentationTypeClosingCreditStart is the segmentation_type_id for
        // Closing Credit Start. Added in ANSI/SCTE 2020.
        SegmentationTypeClosingCreditStart = 0x26,
        // SegmentationTypeClosingCreditEnd is the segmentation_type_id for
        // Closing Credit End. Added in ANSI/SCTE 2020.
        SegmentationTypeClosingCreditEnd = 0x27,
        // SegmentationTypeProviderAdStart is the segmentation_type_id for Provider
        // Ad Start.
        SegmentationTypeProviderAdStart = 0x30,
        // SegmentationTypeProviderAdEnd is the segmentation_type_id for Provider Ad
        // End.
        SegmentationTypeProviderAdEnd = 0x31,
        // SegmentationTypeDistributorAdStart is the segmentation_type_id for
        // Distributor Ad Start.
        SegmentationTypeDistributorAdStart = 0x32,
        // SegmentationTypeDistributorAdEnd is the segmentation_type_id for
        // Distributor Ad End.
        SegmentationTypeDistributorAdEnd = 0x33,
        // SegmentationTypeProviderPOStart is the segmentation_type_id for Provider
        // PO Start.
        SegmentationTypeProviderPOStart = 0x34,
        // SegmentationTypeProviderPOEnd is the segmentation_type_id for Provider PO
        // End.
        SegmentationTypeProviderPOEnd = 0x35,
        // SegmentationTypeDistributorPOStart is the segmentation_type_id for
        // Distributor PO Start.
        SegmentationTypeDistributorPOStart = 0x36,
        // SegmentationTypeDistributorPOEnd is the segmentation_type_id for
        // Distributor PO End.
        SegmentationTypeDistributorPOEnd = 0x37,
        // SegmentationTypeProviderOverlayPOStart is the segmentation_type_id for
        // Provider Overlay Placement Opportunity Start.
        SegmentationTypeProviderOverlayPOStart = 0x38,
        // SegmentationTypeProviderOverlayPOEnd is the segmentation_type_id for
        // Provider Overlay Placement Opportunity End.
        SegmentationTypeProviderOverlayPOEnd = 0x39,
        // SegmentationTypeDistributorOverlayPOStart is the segmentation_type_id for
        // Distributor Overlay Placement Opportunity Start.
        SegmentationTypeDistributorOverlayPOStart = 0x3a,
        // SegmentationTypeDistributorOverlayPOEnd is the segmentation_type_id for
        // Distributor Overlay Placement Opportunity End.
        SegmentationTypeDistributorOverlayPOEnd = 0x3b,
        // SegmentationTypeProviderPromoStart is the segmentation_type_id for
        // Provider Promo Start. Added in ANSI/SCTE 2020.
        SegmentationTypeProviderPromoStart = 0x3c,
        // SegmentationTypeProviderPromoEnd is the segmentation_type_id for
        // Provider Promo End. Added in ANSI/SCTE 2020.
        SegmentationTypeProviderPromoEnd = 0x3d,
        // SegmentationTypeDistributorPromoStart is the segmentation_type_id for
        // Distributor Promo Start. Added in ANSI/SCTE 2020.
        SegmentationTypeDistributorPromoStart = 0x3e,
        // SegmentationTypeDistributorPromoEnd is the segmentation_type_id for
        // Distributor Promo End. Added in ANSI/SCTE 2020.
        SegmentationTypeDistributorPromoEnd = 0x3f,
        // SegmentationTypeUnscheduledEventStart is the segmentation_type_id for
        // Unscheduled Event Start.
        SegmentationTypeUnscheduledEventStart = 0x40,
        // SegmentationTypeUnscheduledEventEnd is the segmentation_type_id for
        // Unscheduled Event End.
        SegmentationTypeUnscheduledEventEnd = 0x41,
        // SegmentationTypeAltConOppStart is the segmentation_type_id for
        // Alternate Content Opportunity Start. Added in ANSI/SCTE 2020.
        SegmentationTypeAltConOppStart = 0x42,
        // SegmentationTypeAltConOppEnd is the segmentation_type_id for
        // Alternate Content Opportunity End. Added in ANSI/SCTE 2020.
        SegmentationTypeAltConOppEnd = 0x43,
        // SegmentationTypeProviderAdBlockStart is the segmentation_type_id for
        // Provider Ad Block Start. Added in ANSI/SCTE 2020.
        SegmentationTypeProviderAdBlockStart = 0x44,
        // SegmentationTypeProviderAdBlockEnd is the segmentation_type_id for
        // Provider Ad Block End. Added in ANSI/SCTE 2020.
        SegmentationTypeProviderAdBlockEnd = 0x45,
        // SegmentationTypeDistributorAdBlockStart is the segmentation_type_id for
        // Distributor Ad Block Start. Added in ANSI/SCTE 2020.
        SegmentationTypeDistributorAdBlockStart = 0x46,
        // SegmentationTypeDistributorAdBlockEnd is the segmentation_type_id for
        // Distributor Ad Block End. Added in ANSI/SCTE 2020.
        SegmentationTypeDistributorAdBlockEnd = 0x47,
        // SegmentationTypeNetworkStart is the segmentation_type_id for Network Start.
        SegmentationTypeNetworkStart = 0x50,
        // SegmentationTypeNetworkEnd is the segmentation_type_id for Network End.
        SegmentationTypeNetworkEnd = 0x51,
    }

}
