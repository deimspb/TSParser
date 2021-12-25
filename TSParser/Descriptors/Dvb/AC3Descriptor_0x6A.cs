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

using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record AC3Descriptor_0x6A : Descriptor
    {
        public bool ComponentTypeFlag { get; }
        public bool BsidFlag { get; }
        public bool MainIdFlag { get; }
        public bool AsvcFlag { get; }
        public byte ComponentType { get; }
        public byte Bsid { get; }
        public byte MainId { get; }
        public byte Asvc { get; }
        public byte[] AdditionalInfoByte { get; } = null!;
        public AC3Descriptor_0x6A(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ComponentTypeFlag = (bytes[pointer] & 0x80) != 0;
            BsidFlag = (bytes[pointer] & 0x40) != 0;
            MainIdFlag = (bytes[pointer] & 0x20) != 0;
            AsvcFlag = (bytes[pointer] & 0x10) != 0;
            pointer++;
            if (ComponentTypeFlag)
            {
                ComponentType = bytes[pointer++];
            }
            if (BsidFlag)
            {
                Bsid = bytes[pointer++];
            }
            if (MainIdFlag)
            {
                Bsid = bytes[pointer++];
            }
            if (AsvcFlag)
            {
                Asvc = bytes[pointer++];
            }
            if (DescriptorLength > pointer)
            {
                Logger.Send(LogStatus.Info, $"Additional info in AC-3 Descriptor");
            }
        }
        public override string ToString()
        {
            string str = $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName},\n";
            str += $"            Compenent type:{ComponentTypeFlag}, component: {ComponentType}, Bsid flag: {BsidFlag}, Bsid: {Bsid}, Main id Flag: {MainIdFlag}, MainId: {MainId}, ASCV flag: {AsvcFlag}, ASVC: {Asvc}";
            return str;
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName},\n";
            str += $"{prefix}Compenent type:{ComponentTypeFlag}, component: {ComponentType}, Bsid flag: {BsidFlag}, Bsid: {Bsid}, Main id Flag: {MainIdFlag}, MainId: {MainId}, ASCV flag: {AsvcFlag}, ASVC: {Asvc}\n";
            return str;
        }

    }
}
