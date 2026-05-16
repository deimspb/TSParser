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

using TSParser.DictionariesData;
using TSParser.Service;

namespace TSParser.Descriptors.Custom
{
    public record SettingsDescriptorV1_0x89 : Descriptor
    {
        public byte ServiceVersionLength { get; }
        public string ServiceVersion { get; }
        public bool ServiceStatus { get; }
        public byte ServiceLastUpdateLength { get; }
        public uint ServiceLastUpdate { get; }
        public DateTime ServiceLastUpdateTime => Utils.UnixTimeStampToDateTime(ServiceLastUpdate);
        public SettingsDescriptorV1_0x89(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            int pointer = 2;
            ServiceVersionLength = bytes[pointer++];
            ServiceVersion = Dictionaries.BytesToString(bytes.Slice(pointer, ServiceVersionLength));
            pointer += ServiceVersionLength;
            ServiceStatus = (bytes[pointer] & 0x80) != 0;
            ServiceLastUpdateLength = (byte)(bytes[pointer++] & 0x7F);
            ServiceLastUpdate = (uint)((bytes[pointer++] << 24) + (bytes[pointer++] << 16) + (bytes[pointer++] << 8) + bytes[pointer++]);
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Service Version: {ServiceVersion}\n";
            str += $"{prefix}Service status: {ServiceStatus}\n";
            str += $"{prefix}Service last update: {ServiceLastUpdateTime}\n";

            return str;
        }
    }
}
