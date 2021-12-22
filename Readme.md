# Transport Stream Parser Library
Parse Transport stream packet,
Adaptation field
Pes header
* DVB tables:
* PAT
* CAT
* PMT
* BAT
* SDT 
* EIT
* TOT
* TDT
* NIT
* Support non zero pointer field

Descriptors:
* 0x09	CaDescriptor_0x09
* 0x0A	Iso639LanguageDescriptor_0x0A
* 0x43  SatelliteDeliverySystemDescriptor_0x43
* 0x44	CableDeliverySystemDescriptor_0x44
* 0x48	ServiceDescriptor_0x48
* 0x4A	LinkageDescriptor_0x4A
* 0x4D	ShortEventDescriptor_0x4D
* 0x4E	ExtendedEventDescriptor_0x4E
* 0x52	StreamIdentifierDescriptor_0x52
* 0x54	ContentDescriptor_0x54
* 0x55	ParentalRatingDescriptor_0x55
* 0x56	TeletextDescriptor_0x56
* 0x5C	MultilingualBouquetNameDescriptor_0x5C
* 0x66	DataBroadcastIdDescriptor_0x66
* 0x6A	AC3Descriptor_0x6A
* 0x6F	ApplicationSignallingDescriptor_0x6F
* 0x13	CarouselIdentifierDescriptor_0x13
* 0x14	AssociationTagDescriptor_0x14
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
 event PatReady OnPatReady 
 event PmtReady OnPmtReady 
 event EitReady OnEitReady 
 event TdtReady OnTdtReady 
 event SdtReady OnSdtReady 
 event BatReady OnBatReady 
 event CatReady OnCatReady 
 event NitReady OnNitReady 

 You can also subcribe to event OnParserComplete which will rase when file is complete or StopParser method calls.

 This project have build in logger class which write logs in debug view on in relese mode you can subscribe to the OnLogMessage event

 You can run parser in async mode:
 ```
 RunParserAsync();
 ```
