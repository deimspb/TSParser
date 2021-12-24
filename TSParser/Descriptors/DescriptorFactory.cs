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

using TSParser.Descriptors.AitDescriptors;
using TSParser.Descriptors.Dvb;
using TSParser.Descriptors.ExtendedDvb;
using TSParser.Service;

namespace TSParser.Descriptors
{
    internal record DescriptorFactory
    {
        private static List<byte> m_unknownDescriptorListId = new List<byte>();
        private static List<byte> m_unknownExtensionDescList = new List<byte>();
        private static List<byte> m_unknownAitDescList = new List<byte>();

        internal static Descriptor GetDescriptor(ReadOnlySpan<byte> bytes, string descAllocation = "")
        {
            try
            {
                switch (bytes[0])
                {
                    case 0x0A: return new Iso639LanguageDescriptor_0x0A(bytes);
                    case 0x56: return new TeletextDescriptor_0x56(bytes);
                    case 0x6A: return new AC3Descriptor_0x6A(bytes);
                    case 0x66: return new DataBroadcastIdDescriptor_0x66(bytes);
                    case 0x09: return new CaDescriptor_0x09(bytes);
                    case 0x54: return new ContentDescriptor_0x54(bytes);
                    case 0x4D: return new ShortEventDescriptor_0x4D(bytes);
                    case 0x4E: return new ExtendedEventDescriptor_0x4E(bytes);
                    case 0x55: return new ParentalRatingDescriptor_0x55(bytes);
                    case 0x7F: return GetExtensionDescriptor(bytes, descAllocation);
                    case 0x40: return new NetworkNameDescriptor_0x40(bytes);
                    case 0x52: return new StreamIdentifierDescriptor_0x52(bytes);
                    case 0x48: return new ServiceDescriptor_0x48(bytes);
                    case 0x6F: return new ApplicationSignallingDescriptor_0x6F(bytes);
                    case 0x43: return new SatelliteDeliverySystemDescriptor_0x43(bytes);
                    case 0x44: return new CableDeliverySystemDescriptor_0x44(bytes);
                    case 0x4A: return new LinkageDescriptor_0x4A(bytes);
                    case 0x47: return new BouquetNameDescriptor_0x47(bytes);
                    case 0x5C: return new MultilingualBouquetNameDescriptor_0x5C(bytes);
                    case 0x13: return new CarouselIdentifierDescriptor_0x13(bytes);
                    case 0x14: return new AssociationTagDescriptor_0x14(bytes);
                    case 0x53: return new CaIdentifierDescriptor_0x53(bytes);
                    case 0x41: return new ServiceListDescriptor_0x41(bytes);
                    case 0x83: return new LogicalChannelNumberDescriptor_0x83(bytes);
                    case 0x5F: return new PrivateDataSpecifierDescriptor_0x5F(bytes);
                    case 0x59: return new SubtitlingDescriptor_0x59(bytes);
                    case 0x0E: return new MaximumBitrateDescriptor_0x0E(bytes);
                    case 0x05: return new RegistrationDescriptor_0x05(bytes);
                    case 0x38: return new HevcVideoDescriptor_0x38(bytes);
                    case 0x0C: return new MultiplexBufferUtilizationDescriptor_0x0C(bytes);
                    case 0x02: return new VideoStreamDescriptor_0x02(bytes);
                    case 0x03: return new AudioStreamDescriptor_0x03(bytes);
                    case 0x06: return new DataStreamAlignmentDescriptor_0x06(bytes);
                    case 0x28: return new AvcVideoDescriptor_0x28(bytes);
                    case 0x58: return new LocalTimeOffsetDescriptor_0x58(bytes);
                    case 0x8A: return new CueIdentifierDescriptor_0x8A(bytes);
                    case 0x0F: return new PrivateDataIndicatorDescriptor_0x0F(bytes);
                    case 0x45: return new VbiDataDescriptor_0x45(bytes);
                    case 0x7C: return new AACDescriptor_0x7C(bytes);
                    case 0x11: return new StdDescriptor_0x11(bytes);
                    case 0x70: return new AdaptationFieldDataDescriptor_0x70(bytes);
                    case 0x2A: return new AvcTimingAndHrdDescriptor_0x2A(bytes);
                    default:
                        {
                            if (!m_unknownDescriptorListId.Contains(bytes[0]))
                            {
                                Logger.Send(LogStatus.Info, $"Not specified descriptor with tag: 0x{bytes[0]:X2}, descriptor location: {descAllocation}");
                                m_unknownDescriptorListId.Add(bytes[0]);
                            }

                            return new Descriptor(bytes);
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.Exception, $"While creating descriptor tag: 0x{bytes[0]:X2} descriptor location: {descAllocation}", ex);
                return new Descriptor(bytes);
            }
        }
        internal static Descriptor GetAitDescriptor(ReadOnlySpan<byte> bytes, string descAllocation = "")
        {
            try
            {
                switch (bytes[0])
                {
                    case 0x00: return new ApplicationDescriptor_0x00(bytes);
                    case 0x01: return new ApplicationNameDescriptor_0x01(bytes);
                    case 0x02: return new TransportProtocolDescriptor_0x02(bytes);
                    case 0x15: return new SimpleApplicationLocationDescriptor_0x15(bytes);
                    default:
                        {
                            if (!m_unknownAitDescList.Contains(bytes[0]))
                            {
                                Logger.Send(LogStatus.Info, $"Not specified AIT descriptor with tag: 0x{bytes[0]:X2}, descriptor location: {descAllocation}");
                                m_unknownAitDescList.Add(bytes[0]);
                            }

                            return new AitDescriptor(bytes);
                        }
                }
            }
            catch(Exception ex)
            {
                Logger.Send(LogStatus.Exception, $"While creating AIT descriptor tag: 0x{bytes[0]:X2} descriptor location: {descAllocation}", ex);
                return new AitDescriptor(bytes);
            }
            
        }
        internal static Descriptor GetExtensionDescriptor(ReadOnlySpan<byte> bytes, string descAllocation = "")
        {
            try
            {
                switch (bytes[2])
                {
                    case 0x00: return new ImageIconDescriptor_0x00(bytes);
                    default:
                        {
                            if (!m_unknownExtensionDescList.Contains(bytes[2]))
                            {
                                Logger.Send(LogStatus.Info, $"Not specified extension descriptor with tag: 0x{bytes[2]:X2}, descriptor location: {descAllocation}");
                                m_unknownExtensionDescList.Add(bytes[2]);
                            }

                            return new ExtensionDescriptor_0x7F(bytes);
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.Exception, $"While creating extension descriptor tag: 0x{bytes[1]:X2} descriptor location: {descAllocation}", ex);
                return new ExtensionDescriptor_0x7F(bytes);
            }
        }
        internal static List<Descriptor> GetDescriptorList(ReadOnlySpan<byte> bytes, string descAllocation = "", byte callerTableId = 0)
        {
            switch (callerTableId)
            {
                case 0x74:
                    {
                        return GetAitDescriptorList(bytes, descAllocation);
                    }
                default:
                    {
                        return GetDvbDescriptorList(bytes, descAllocation);
                    }
            }
        }        
        internal static List<Descriptor> GetDvbDescriptorList(ReadOnlySpan<byte> bytes, string descAllocation = "")
        {
            var pointer = 0;
            List<Descriptor> descriptors = new List<Descriptor>();
            while (pointer < bytes.Length)
            {
                var desc = GetDescriptor(bytes[pointer..], descAllocation);
                descriptors.Add(desc);
                pointer += desc.DescriptorLength + 2;
            }
            return descriptors;
        }
        internal static List<Descriptor> GetAitDescriptorList(ReadOnlySpan<byte> bytes, string descAllocation = "")
        {
            var pointer = 0;
            List<Descriptor> descriptors = new List<Descriptor>();
            while (pointer < bytes.Length)
            {
                var desc = GetAitDescriptor(bytes[pointer..], descAllocation);
                descriptors.Add(desc);
                pointer += desc.DescriptorLength + 2;
            }
            return descriptors;
        }
        
    }
}
