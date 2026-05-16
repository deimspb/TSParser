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

using System.Buffers.Binary;
using TSParser.Service;

namespace TSParser.Descriptors.Custom
{
    public record CaDescriptorCustom_0x09 : Descriptor
    {
        public ushort CaSystemId { get; }
        public ushort CaPid { get; }
        public byte[] PrivateDateBytes { get; }
        public byte CaProviderId { get; }
        public string CaProviderName => GetProviderIdName(CaProviderId);
        public byte CaServiceId { get; }
        public string CaServiceIdName => GetServiceIdName(CaServiceId);

        public CaDescriptorCustom_0x09(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            CaSystemId = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            if (CaSystemId != 0x2710 && CaSystemId != 0x4ae1 && CaSystemId != 0x4ae0)
            {
                throw new Exception("Non dre cas");
            }
            CaPid = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) & 0x1FFF);
            PrivateDateBytes = new byte[DescriptorLength - 4];
            bytes.Slice(6, PrivateDateBytes.Length).CopyTo(PrivateDateBytes);
            CaProviderId = PrivateDateBytes[0];
            if (PrivateDateBytes.Length > 1)
            {
                CaServiceId = PrivateDateBytes[1];
            }
            
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header} DRE CA: 0x{CaSystemId:X2}, CA pid: {CaPid}, CA provider id: {CaProviderName}, CA service id: {CaServiceId}, private data: {BitConverter.ToString(PrivateDateBytes):X}\n";
        }
        private string GetProviderIdName(byte bt)
        {
            switch (bt)
            {
                case 0x11: return "CAS 2.2 / CAS 2.1";
                case 0x14: return "CAS 2.0 Sib";
                case 0x02: return "CAS 3.0";
                case 0x19: return "CAS 4.1";
                case 0x18: return "CAS 4.2";
                case 0x03: return "CAS 3.0 Sib";
                case 0x1A: return "CAS 4.0 Sib";
                case 0x28: return "CAS 5.0";
                case 0x2C: return "CAS 5.5";
                default: return "Unknown dre cas";
            }
        }
        private string GetServiceIdName(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "simple EMM/ECM";
                case 0x01: return "Info cas old + TVMail old";
                case 0x02: return "Info banner old";
                case 0x05: return "Switch banner metadata";
                case 0x06: return "Switch banner content";
                case 0x09: return "Info banner new metadata";
                case 0x0A: return "Info banner new content";
                case 0x0D: return "R2 control subscription";
                case 0x10: return "Info cas new";
                case 0x11: return "TVMail new metadata";
                case 0x12: return "TVMail new data";
                case 0x18: return "ABOX";
                default: return "Unknown service id";
            }
        }

    }
}
