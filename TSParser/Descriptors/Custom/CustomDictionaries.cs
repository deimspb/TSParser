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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSParser.Descriptors.Custom
{
    internal class CustomDictionaries
    {
        internal static string GetStbName(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "All Stb";
                case 0x01: return "U510";
                case 0x02: return "U210";
                case 0x03: return "U210CI";
                case 0x04: return "B210";
                case 0x05: return "E501";
                case 0x06: return "C591";
                case 0x07: return "C5911";
                case 0x08: return "E212";
                case 0x09: return "E502";
                case 0x0a: return "B211 & B212";
                case 0x0b: return "C211";
                case 0x0c: return "A230";
                case 0x0d: return "E521L";
                case 0x0e: return "B520 & B522";
                case 0x0f: return "B521";
                case 0x10: return "B531M & B532M";
                case 0x11: return "B533 & B534M & B531N";
                case 0x12: return "C592";
                case 0x13: return "B521H & B521HL";
                case 0x14: return "B5310 & B5311";
                case 0x15: return "B527, B528";
                case 0x16: return "B621, B622";
                case 0x17: return "B5210, B5211";
                case 0x18: return "B626L";
                case 0x19: return "B529L";
                case 0x1A: return "B623L";
                case 0x1B: return "B523L";
                case 0x64: return "TV without HEVC support";
                case 0x65: return "TV with HEVC support";
                case byte n when (n >= 0x6e && n <= 0x77): return "CAM";
                default: return $"Uknown stb id: {bt}";
            }
        }
    }
}
