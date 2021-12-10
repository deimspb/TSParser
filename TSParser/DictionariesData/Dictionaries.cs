using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSParser.DictionariesData
{
    internal class Dictionaries
    {
        internal static string GetStreamIdName(byte bt)
        {
            switch (bt)
            {
                case 0b10111100: return "program_stream_map";
                case 0b10111101: return "private_stream_1";
                case 0b10111111: return "private_stream_2";
                case byte n when (bt & 0xE0) == 0b110: return $"ISO/IEC 13818-3 or ISO/IEC 11172-3 or ISO/IEC 13818-7 or ISO/IEC 14496-3 or ISO/IEC 23008-3 audio stream number {bt & 0x1F}";
                case byte n when (bt & 0xF0) == 0b1110: return $"Rec. ITU-T H.262 | ISO/IEC 13818-2, ISO/IEC 11172-2, ISO/IEC 14496-2, Rec. ITU-T H.264 | ISO/IEC 14496-10, Rec. ITU-T H.265 | ISO/IEC 23008-2, Rec. ITU-T H.266 | ISO/IEC 23090-3 or ISO/IEC 23094-1 video stream number {bt & 0x0F}";
                case 0b11110000: return "ECM_stream";
                case 0b11110001: return "EMM_stream";
                case 0b11110010: return "Rec. ITU-T H.222.0 | ISO/IEC 13818-1 Annex A or ISO/IEC 13818-6_DSMCC_stream";
                case 0b11110011: return "ISO/IEC_13522_stream";
                case 0b11110100: return "Rec. ITU-T H.222.1 type A";
                case 0b11110101: return "Rec. ITU-T H.222.1 type B";
                case 0b11110110: return "Rec. ITU-T H.222.1 type C";
                case 0b11110111: return "Rec. ITU-T H.222.1 type D";
                case 0b11111000: return "Rec. ITU-T H.222.1 type E";
                case 0b11111001: return "ancillary_stream";
                case 0b11111010: return "ISO/IEC 14496-1_SL-packetized_stream";
                case 0b11111011: return "ISO/IEC 14496-1_FlexMux_stream";
                case 0b11111100: return "metadata stream";
                case 0b11111101: return "extended_stream_id";
                case 0b11111110: return "reserved data stream";
                case 0b11111111: return "program_stream_directory";
                default: return "unknown stream id";
            }
        }
    }
}
