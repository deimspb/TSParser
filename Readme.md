# DVB MPEG Transport Stream Parser Library
Parse Transport stream packet,
Adaptation field
Pes header

DVB tables:
* PAT
* CAT
* PMT
* BAT
* SDT 
* EIT
* TOT
* TDT
* NIT
* AIT
* Support non zero pointer field

Descriptors:
 * 0x02 VideoStreamDescriptor_0x02
 * 0x03 AudioStreamDescriptor_0x03
 * 0x05 RegistrationDescriptor_0x05
 * 0x06 DataStreamAlignmentDescriptor_0x06
 * 0x09 CaDescriptor_0x09
 * 0x0A Iso639LanguageDescriptor_0x0A
 * 0x0C MultiplexBufferUtilizationDescriptor_0x0C
 * 0x0E MaximumBitrateDescriptor_0x0E
 * 0x11 StdDescriptor_0x11
 * 0x13 CarouselIdentifierDescriptor_0x13
 * 0x14 AssociationTagDescriptor_0x14
 * 0x28 AvcVideoDescriptor_0x28
 * 0x2A AvcTimingAndHrdDescriptor_0x2A
 * 0x38 HevcVideoDescriptor_0x38
 * 0x40 NetworkNameDescriptor_0x40
 * 0x41 ServiceListDescriptor_0x41
 * 0x43 SatelliteDeliverySystemDescriptor_0x43
 * 0x44 CableDeliverySystemDescriptor_0x44
 * 0x45 VbiDataDescriptor_0x45
 * 0x47 BouquetNameDescriptor_0x47
 * 0x48 ServiceDescriptor_0x48
 * 0x4A LinkageDescriptor_0x4A
 * 0x4D ShortEventDescriptor_0x4D
 * 0x4E ExtendedEventDescriptor_0x4E
 * 0x52 StreamIdentifierDescriptor_0x52
 * 0x53 CaIdentifierDescriptor_0x53
 * 0x54 ContentDescriptor_0x54
 * 0x55 ParentalRatingDescriptor_0x55
 * 0x56 TeletextDescriptor_0x56
 * 0x58 LocalTimeOffsetDescriptor_0x58
 * 0x59 SubtitlingDescriptor_0x59
 * 0x5A TerrestrialDeliverySystemDescriptor_0x5A
 * 0x5C MultilingualBouquetNameDescriptor_0x5C
 * 0x64 DataBroadcastDescriptor_0x64
 * 0x66 DataBroadcastIdDescriptor_0x66
 * 0x6A AC3Descriptor_0x6A
 * 0x6F ApplicationSignallingDescriptor_0x6F
 * 0x70 AdaptationFieldDataDescriptor_0x70
 * 0x7F ExtensionDescriptor_0x7F
 * 0x83 LogicalChannelNumberDescriptor_0x83
 * 0x8A CueIdentifierDescriptor_0x8A
 * 0x7C AACDescriptor_0x7C

Extension Descriptor:
* 0x00  ImageIconDescriptor_0x00

AIT Descriptor:
* 0x00	ApplicationDescriptor_0x00
* 0x01	ApplicationNameDescriptor_0x01
* 0x02	TransportProtocolDescriptor_0x02
* 0x15	SimpleApplicationLocationDescriptor_0x15

## How to use this parser
To read from file:
```
string tsFile = @"....TsFile.ts";
parser = new TsParser(tsFile);
parser.RunParser();
```
To get ts pacjets from udp stream:
```
parser = new TsParser("239.1.1.27", 1234, "192.168.99.239");
parser.RunParser();
```
where 239.1.1.27 udp multicast destination address
1234 udp multicast destination port
192.168.99.239 your network adapter address 

To Get tables you need to subscribe to the events.
 each table have it's own event:
 * event PatReady OnPatReady 
 * event PmtReady OnPmtReady 
 * event EitReady OnEitReady 
 * event TdtReady OnTdtReady 
 * event SdtReady OnSdtReady 
 * event BatReady OnBatReady 
 * event CatReady OnCatReady 
 * event NitReady OnNitReady 
 * event AitReady OnAitReady

 You can also subcribe to event OnParserComplete which will rase when file is complete or StopParser method calls.

 This project have build in logger class which write logs in debug view on in relese mode you can subscribe to the OnLogMessage event

 You can run parser in async mode:
 ```
 RunParserAsync();
 ```
 ## Log output example
 ```
 [24.12.2021 13:42:29.826] [INFO] [Start ts file .....\9_scr_sync.ts parsing] 
 [24.12.2021 13:42:30.010] [INFO] [Not specified descriptor with tag: 0xC0, descriptor location: Table: NIT, table id: 64, section number: 1] 
 [24.12.2021 13:42:30.148] [INFO] [Not specified descriptor with tag: 0x89, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
 [24.12.2021 13:42:30.150] [INFO] [Not specified descriptor with tag: 0x90, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
 [24.12.2021 13:42:30.152] [INFO] [Not specified descriptor with tag: 0xB0, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
 [24.12.2021 13:42:30.157] [INFO] [Not specified descriptor with tag: 0xB1, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
 [24.12.2021 13:42:30.181] [INFO] [Not specified AIT descriptor with tag: 0x02, descriptor location: Table: AIT, pid: 1565, App identifier: 0x2778] 
 [24.12.2021 13:42:30.186] [INFO] [Not specified AIT descriptor with tag: 0x01, descriptor location: Table: AIT, pid: 1565, App identifier: 0x2778] 
 [24.12.2021 13:42:30.192] [INFO] [Not specified AIT descriptor with tag: 0x15, descriptor location: Table: AIT, pid: 1565, App identifier: 0x2778] 
 [24.12.2021 13:42:30.311] [INFO] [Not specified descriptor with tag: 0x91, descriptor location: Table: EIT, Service id: 9005, Event id: 29676] 
 [24.12.2021 13:42:33.245] [INFO] [Parser complete working] 
 ```
 ## Library features

 * Logger send info about not specified descriptors, but do it only one time for each not specified descriptor.


 ## TODO list:
 * Add simple TR 101 290 first priority errors logs
 * Add NAL unit parsing
 * Add ISDB support
 * Add ATSC support
 * Add Plp support
 * Add teletext parsing
 * Add subtitling parsing
 * Add SCTE35 support
 * Add DekTec PCI/USB adapters support
