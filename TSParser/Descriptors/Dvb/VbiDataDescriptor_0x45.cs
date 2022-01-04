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
    public record VbiDataDescriptor_0x45 : Descriptor
    {
        public List<DataService> DataServices { get; }
        public VbiDataDescriptor_0x45(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            DataServices = new List<DataService>();
            var pointer = 2;
            while (pointer < DescriptorLength)
            {
                DataService ds = new DataService(bytes[pointer..]);
                pointer += ds.DataServiceDescriptorLength + 2;
                DataServices.Add(ds);
            }
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);

            string str = $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (DataService ds in DataServices)
            {
                str += ds.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct DataService
    {
        public byte DataServiceId { get; }
        public string DataServiceName => GetDataServiceName(DataServiceId);
        public byte DataServiceDescriptorLength { get; }
        public VbiLine[] VbiLines { get; } = null!;
        public DataService(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            DataServiceId = bytes[pointer++];
            DataServiceDescriptorLength = bytes[pointer++];
            if (DataServiceId == 0x01 || DataServiceId == 0x02 ||
                DataServiceId == 0x04 || DataServiceId == 0x05 ||
                DataServiceId == 0x06 || DataServiceId == 0x07)
            {
                VbiLines = new VbiLine[DataServiceDescriptorLength];
                for (int i = 0; i < DataServiceDescriptorLength; i++)
                {
                    VbiLines[i] = new VbiLine(bytes[pointer++]);
                }
            }
            else
            {
                //reserved
            }
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{header}Data Service Id: {DataServiceId}\n";
            str += $"{prefix}Data Service name: {DataServiceName}\n";
            foreach (var item in VbiLines)
            {
                str += item.Print(prefixLen + 4);
            }
            return str;
        }
        private string GetDataServiceName(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "reserved for future use";
                case 0x01: return "EBU teletext (Requires additional teletext_descriptor)";
                case 0x02: return "inverted teletext";
                case 0x03: return "reserved";
                case 0x04: return "VPS";
                case 0x05: return "WSS";
                case 0x06: return "Closed Caption";
                case 0x07: return "monochrome 4:2:2 samples";
                case byte n when n >= 0x08 && n <= 0xEF: return "reserved for future use";
                default: return "user defined";
            }
        }
    }
    public struct VbiLine
    {
        public bool FieldParity { get; }
        public byte LineOffset { get; }
        public VbiLine(byte bt)
        {
            //reserved 2 bits
            FieldParity = (bt & 0x20) != 0;
            LineOffset = (byte)(bt & 0x1F);
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Field parity: {FieldParity}, Line offset: {LineOffset}\n";
        }
    }
}
