# DVB MPEG Transport Stream Parser Library
...project not finished

Parse Transport stream packet,
Adaptation field
Pes header

DVB/MPEG tables:
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
* MIP
* SCTE35
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
 * 0x50 ComponentDescriptor_0x50
 * 0x52 StreamIdentifierDescriptor_0x52
 * 0x53 CaIdentifierDescriptor_0x53
 * 0x54 ContentDescriptor_0x54
 * 0x55 ParentalRatingDescriptor_0x55
 * 0x56 TeletextDescriptor_0x56
 * 0x58 LocalTimeOffsetDescriptor_0x58
 * 0x59 SubtitlingDescriptor_0x59
 * 0x5A TerrestrialDeliverySystemDescriptor_0x5A
 * 0x5C MultilingualBouquetNameDescriptor_0x5C
 * 0x60 ServiceMoveDescriptor_0x60
 * 0x64 DataBroadcastDescriptor_0x64
 * 0x66 DataBroadcastIdDescriptor_0x66
 * 0x6A AC3Descriptor_0x6A
 * 0x6C CellListDescriptor_0x6C
 * 0x6D CellFrequencyLinkDescriptor_0x6D
 * 0x6F ApplicationSignallingDescriptor_0x6F
 * 0x70 AdaptationFieldDataDescriptor_0x70
 * 0x7F ExtensionDescriptor_0x7F
 * 0x83 LogicalChannelNumberDescriptor_0x83
 * 0x8A CueIdentifierDescriptor_0x8A
 * 0x7C AACDescriptor_0x7C


Extension Descriptor:
* 0x00  ImageIconDescriptor_0x00
* 0x04	T2DeliverySystemDescriptor_0x04


AIT Descriptor:
* 0x00	ApplicationDescriptor_0x00
* 0x01	ApplicationNameDescriptor_0x01
* 0x02	TransportProtocolDescriptor_0x02
* 0x03	DvbJApplicationDescriptor_0x03
* 0x04	DvbJApplicationLocationDescriptor_0x04
* 0x10	ApplicationStorageDescriptor_0x10
* 0x15	SimpleApplicationLocationDescriptor_0x15

Full support of SCTE35 tables, add functionality to serialize scte35 table to XML as describe in http://www.scte.org/schemas/35


## How to use this parser
First you need to create config instance (this examlpe for udp multicast)
```
var config = new ParserConfig
{
    MulticastGroup = sopts.MulticastAddress,
    MulticastPort = sopts.UdpPort,
    MulticastIncomingIp = sopts.McastInterfaceAddress,
    ParserRunTime = sopts.runTime == 0 ? m_defaultDuration : sopts.runTime,
};
```
Than create instance of parser with this config:
```
parser = new(config); 
```
next step:
```
parser.OnParserComplete += Parser_OnParserComplete;
parser.OnPatReady += Parser_OnPatReady;
parser.OnPmtReady += Parser_OnPmtReady;
parser.OnSdtReady += Parser_OnSdtReady;
parser.OnNitReady += Parser_OnNitReady;
parser.OnBatReady += Parser_OnBatReady;
parser.OnCatReady += Parser_OnCatReady;
parser.OnAitReady += Parser_OnAitReady;
parser.OnEitReady += Parser_OnEitReady;
parser.OnMipReady += Parser_OnMipReady;
parser.OnTdtReady += Parser_OnTdtReady;
parser.OnTotready += Parser_OnTotready;
parser.OnTsPacketReady += Parser_OnTsPacketReady;
parser.OnScte35Ready += Parser_OnScte35Ready;
Logger.OnLogMessage += Logger_OnLogMessage;
```
next:
```
parser.RunParser();
```

Or you can push tables from DekTec analyzer:
```
byte[] dataBuffer = new byte[m_packetSize * 100];

m_dtInp.SetRxControl(DTAPI.RXCTRL_RCV);

while (!Done)
{
    m_dtInp.Read(dataBuffer, dataBuffer.Length);
    parser.PushBytes(dataBuffer, m_packetSize);
}
```
In this case you won't needed to call parser.RunParser() method
just subscribe to events

### Parser Modes:
* DVB
* ISDB (not implement yet)
* ATSC (not implement yet)

### Decoder Modes:
* Table
in this mode you can get decoded tables via events

* Packet 
in this mode you can get only decoded ts packets via events. it is work faster than table mode

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
 * event MipReady OnMipReady
 * event Scte35Ready OnScte35Ready

 You can also subcribe to event OnParserComplete which will rase when file is complete or StopParser method calls.

 This project have build in logger class which write logs in debug view on in relese mode you can subscribe to the OnLogMessage event

 You can run parser in async mode:
 ```
 RunParserAsync();
 ```
 ## Log output example
 ```
[05.01.2022 20:17:46.227] [NotImplement] [Not specified descriptor with tag: 0x91, descriptor location: Table: EIT, Service id: 4, Event id: 52122] 
[05.01.2022 20:17:46.229] [INFO] [PCR base pid selected: 271] 
[05.01.2022 20:17:46.270] [NotImplement] [Not specified descriptor with tag: 0xFE, descriptor location: Table: PMT, Program: 27120, Es pid: 1101] 
[05.01.2022 20:17:46.281] [NotImplement] [Not specified descriptor with tag: 0xC0, descriptor location: Table: NIT, table id: 64, section number: 1] 
[05.01.2022 20:17:46.299] [NotImplement] [Not specified descriptor with tag: 0xB4, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.302] [NotImplement] [Not specified descriptor with tag: 0x87, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.304] [NotImplement] [Not specified descriptor with tag: 0xB2, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.307] [NotImplement] [Not specified descriptor with tag: 0x89, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.309] [NotImplement] [Not specified descriptor with tag: 0x90, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.311] [NotImplement] [Not specified descriptor with tag: 0xB0, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.313] [NotImplement] [Not specified descriptor with tag: 0xB1, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.315] [NotImplement] [Not specified descriptor with tag: 0x88, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.317] [NotImplement] [Not specified descriptor with tag: 0xB3, descriptor location: Table: BAT, bouquet id: 1, section number: 0] 
[05.01.2022 20:17:46.367] [NotImplement] [Not specified descriptor with tag: 0x86, descriptor location: Table: BAT, bouquet id: 16, section number: 0] 
[05.01.2022 20:17:46.691] [ETSI] [CC detect om pid: 2004, Total CC for this pid: 1] 
[05.01.2022 20:17:48.834] [ETSI] [CC detect om pid: 5899, Total CC for this pid: 1] 
[05.01.2022 20:17:48.837] [INFO] [Parser complete working]  
 ```
 ##

 ## Library features

 * Logger send info about not implement descriptors, but do it only one time for each not specified descriptor.

 ## Simple analyzer
 * Check all pids in transport stream for CC errors
 * Calculate rate for all pids. For this calculation selected first PID with adaptation field and PCR value. Rate calculate packets between PCR
 rate = (delta_packets)*188 * 8 / (Current_PCR - Last_PCR). Use gate > 100 ms.

 ## TODO list: 
 * Add NAL unit parsing
 * Add ISDB support
 * Add ATSC support
 * Add Plp support
 * Add teletext parsing
 * Add subtitling parsing
 * Add DekTec PCI/USB adapters support
 * Add Test to all objects
 * Add functionality to add custom descriptors
 * Add custom exceptions
