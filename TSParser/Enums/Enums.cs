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
}
