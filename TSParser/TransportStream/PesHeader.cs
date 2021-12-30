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

using System.Buffers.Binary;
using TSParser.DictionariesData;
using TSParser.Enums;
using TSParser.Service;

namespace TSParser.TransportStream
{
    public readonly struct PesHeader
    {
        public const int PACKET_START_CODE_PREFIX = 0x000001;
        public readonly byte StreamId { get; } = default;
        public readonly string StreamIdName => Dictionaries.GetStreamIdName(StreamId);
        public readonly ushort PESPacketLength { get; } = default;
        public readonly byte PESScramblingControl { get; } = default;
        public readonly bool PESPriority { get; } = default;
        public readonly bool DataAlignmentIndicator { get; } = default;
        public readonly bool Copyright { get; } = default;
        public readonly bool OriginalOrCopy { get; } = default;
        public readonly byte PTSDTSFlags { get; } = default;
        public readonly bool ESCRFlag { get; } = default;
        public readonly ulong ESCRBase { get; } = default;
        public readonly ulong ESCRExtension { get; } = default;
        public readonly ulong ESCRValue { get; } = default;
        public readonly bool ESRateFlag { get; } = default;
        public readonly bool DSMTrickModeFlag { get; } = default;
        public readonly bool AdditionalCopyInfoFlag { get; } = default;
        public readonly bool PESCRCFlag { get; } = default;
        public readonly bool PESExtensionFlag { get; } = default;
        public readonly bool PesPrivateDataFlag { get; } = default;
        public readonly bool PackHeaderFieldFlag { get; } = default;
        public readonly bool ProgramPacketSequenceCounterFlag { get; } = default;
        public readonly bool PStdBufferFlag { get; } = default;
        public readonly bool PesExtensionFlag2 { get; } = default;
        public readonly byte[] PesPrivateData { get; } = Array.Empty<byte>();
        public readonly byte PackFieldLength { get; } = default;
        public readonly byte ProgramPacketCounterSequenceCounter { get; } = default;
        public readonly bool Mpeg1Mpeg2Identifier { get; } = default;
        public readonly byte OriginalStuffLength { get; } = default;
        public readonly bool PStdBufferScale { get; } = default;
        public readonly ushort PStdBufferSize { get; } = default;
        public readonly byte PesExtensionFieldLength { get; } = default;
        public readonly bool StreamIdExtensionFlag { get; } = default;
        public readonly byte StreamIdExtension { get; } = default;
        public readonly bool TrefExtensionFlag { get; } = default;
        public readonly byte PESHeaderDataLength { get; } = default;
        public readonly ulong PTSHex { get; } = default;
        public readonly TimeSpan PTSTime => Utils.GetPtsDtsValue(PTSHex);
        public readonly ulong DTSHex { get; } = default;
        public readonly TimeSpan DTSTime => Utils.GetPtsDtsValue(DTSHex);
        public readonly uint ESRate { get; } = default;

        public PesHeader(ReadOnlySpan<byte> bytes, out int pointer)
        {
            pointer = 0;
            StreamId = bytes[pointer++];
            PESPacketLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(pointer, 2));
            pointer += 2;

            if (StreamId != (byte)PesStreamId.Program_stream_map &&
                StreamId != (byte)PesStreamId.Padding_stream &&
                StreamId != (byte)PesStreamId.Private_stream_2 &&
                StreamId != (byte)PesStreamId.ECM_stream &&
                StreamId != (byte)PesStreamId.EMM_stream &&
                StreamId != (byte)PesStreamId.Program_stream_directory &&
                StreamId != (byte)PesStreamId.DSMCC_stream &&
                StreamId != (byte)PesStreamId.H_222_1_type_E)
            {
                //10
                PESScramblingControl = (byte)((bytes[pointer] & 0x30) >> 4);
                PESPriority = (bytes[pointer] & 0x08) != 0;
                DataAlignmentIndicator = (bytes[pointer] & 0x04) != 0;
                Copyright = (bytes[pointer] & 0x02) != 0;
                OriginalOrCopy = (bytes[pointer++] & 0x01) != 0;
                //next byte
                PTSDTSFlags = (byte)((bytes[pointer] & 0xC0) >> 6);
                ESCRFlag = (bytes[pointer] & 0x20) != 0;
                ESRateFlag = (bytes[pointer] & 0x10) != 0;
                DSMTrickModeFlag = (bytes[pointer] & 0x08) != 0;
                AdditionalCopyInfoFlag = (bytes[pointer] & 0x04) != 0;
                PESCRCFlag = (bytes[pointer] & 0x02) != 0;
                PESExtensionFlag = (bytes[pointer++] & 0x01) != 0;
                //next byte
                PESHeaderDataLength = bytes[pointer++];
                //next byte

                if (PTSDTSFlags == 0b10)
                {
                    //0010 
                    PTSHex = Utils.GetPtsDts(bytes.Slice(pointer, 5));
                    pointer += 5;
                }

                if (PTSDTSFlags == 0b11)
                {
                    //0010
                    PTSHex = Utils.GetPtsDts(bytes.Slice(pointer, 5));
                    pointer += 5;
                    //0001
                    DTSHex = Utils.GetPtsDts(bytes.Slice(pointer, 5));
                    pointer += 5;
                }

                if (ESCRFlag)
                {
                    //TODO: need to implement
                    Logger.Send(LogStatus.NotImplement, $"Not impement ESCR");
                    //throw new Exception($"Not impement ESCR");
                }

                if (ESRateFlag)
                {
                    //TODO: check this
                    ESRate = Utils.GetEsRate(bytes.Slice(pointer, 3));
                    pointer += 3;
                }

                if (DSMTrickModeFlag)
                {
                    //TODO: nees to impement
                    Logger.Send(LogStatus.NotImplement, $"Not impement DSM trick");
                    //throw new NotImplementedException($"Not impement DSM trick");
                }

                if (AdditionalCopyInfoFlag)
                {
                    //TODO: nees to impement
                    Logger.Send(LogStatus.NotImplement, $"Not impement Additional copy");
                    //throw new NotImplementedException($"Not impement Additional copy");

                }

                if (PESCRCFlag)
                {
                    //TODO: nees to impement
                    Logger.Send(LogStatus.NotImplement, $"Not impement PES CRC");
                    //throw new NotImplementedException($"Not impement PES CRC");
                }

                if (PESExtensionFlag)
                {
                    PesPrivateDataFlag = (bytes[pointer] & 0x80) != 0;
                    PackHeaderFieldFlag = (bytes[pointer] & 0x40) != 0;
                    ProgramPacketSequenceCounterFlag = (bytes[pointer] & 0x20) != 0;
                    PStdBufferFlag = (bytes[pointer] & 0x10) != 0;
                    // reserved 3 bits
                    PesExtensionFlag2 = (bytes[pointer++] & 0x01) != 0;

                    if (PesPrivateDataFlag)
                    {
                        PesPrivateData = new byte[16];
                        bytes.Slice(pointer, 16).CopyTo(PesPrivateData);
                        pointer += 16;
                    }

                    if (PackHeaderFieldFlag)
                    {
                        PackFieldLength = bytes[pointer++];
                        Logger.Send(LogStatus.NotImplement, $"Not implement Pack header");
                        //TODO: implement pack header
                        //throw new NotImplementedException($"Not implement Pack header");
                    }

                    if (ProgramPacketSequenceCounterFlag)
                    {
                        //marker bit
                        ProgramPacketCounterSequenceCounter = (byte)(bytes[pointer++] & 0x7F);
                        //marker bit
                        Mpeg1Mpeg2Identifier = (bytes[pointer] & 0x04) != 0;
                        OriginalStuffLength = (byte)(bytes[pointer++] & 0x3F);
                    }

                    if (PStdBufferFlag)
                    {
                        //01
                        PStdBufferScale = (bytes[pointer] & 0x20) != 0;
                        PStdBufferSize = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(pointer, 2)) & 0x1FFF);
                        pointer += 2;
                    }

                    if (PesExtensionFlag2)
                    {
                        // marker bit 1
                        PesExtensionFieldLength = (byte)(bytes[pointer] & 0x7F);
                        StreamIdExtensionFlag = (bytes[pointer] & 0x80) != 0;
                        if (StreamIdExtensionFlag)
                        {
                            StreamIdExtension = (byte)(bytes[pointer++] & 0x7F);
                        }
                        else
                        {
                            // reserved 6 bits
                            pointer++;
                            TrefExtensionFlag = (bytes[pointer] & 0x02) != 0;
                            if (!TrefExtensionFlag)
                            {
                                Logger.Send(LogStatus.NotImplement, $"Not impement TREF extension");
                                //throw new NotImplementedException($"Not impement TREF extension"); //TODO: impement TREF Extension
                            }

                        }


                    }
                }


            }
            else if (StreamId == (byte)PesStreamId.Program_stream_directory &&
                     StreamId == (byte)PesStreamId.Private_stream_2 &&
                     StreamId == (byte)PesStreamId.ECM_stream &&
                     StreamId == (byte)PesStreamId.EMM_stream &&
                     StreamId == (byte)PesStreamId.Program_stream_directory &&
                     StreamId == (byte)PesStreamId.DSMCC_stream &&
                     StreamId == (byte)PesStreamId.H_222_1_type_E)
            {
                //TODO: impement padding byte
                //throw new NotImplementedException($"Not impement PES Extension");
                Logger.Send(LogStatus.NotImplement, $"Not impement PES Extension");
            }
            else if (StreamId == (byte)PesStreamId.Padding_stream)
            {
                // 1111 1111 bits
            }

            pointer = PESHeaderDataLength; //TODO: check this
        }
    }
}
