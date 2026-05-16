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
    public record SettingsDescriptorV4_0xB1 : Descriptor
    {
        public byte ServiceVersionLength { get; }
        public string ServiceVersion { get; }
        public bool ServiceStatus { get; }
        public byte ServiceUpdateType { get; }
        public string ServiceUpdateTypeStr => GetUpdateStr(ServiceUpdateType);
        public ushort ServiceId { get; }
        public ushort OriginalNetworkId { get; }
        public ushort TransportStreamId { get; }
        public uint ServiceLastUpdate { get; }
        public DateTime ServiceLastUpdateTime => Utils.UnixTimeStampToDateTime(ServiceLastUpdate);
        public byte StbIdLength { get; }
        public StbId[] StbIds { get; }
        public SettingsDescriptorV4_0xB1(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ServiceVersionLength = bytes[pointer++];
            ServiceVersion = Dictionaries.BytesToString(bytes.Slice(pointer, ServiceVersionLength));
            pointer += ServiceVersionLength;
            ServiceStatus = (bytes[pointer] & 0x80) != 0;
            ServiceUpdateType = (byte)(bytes[pointer++] & 0x7F);
            ServiceId = (ushort)((bytes[pointer++] << 8) + bytes[pointer++]);
            OriginalNetworkId = (ushort)((bytes[pointer++] << 8) + bytes[pointer++]);
            TransportStreamId = (ushort)((bytes[pointer++] << 8) + bytes[pointer++]);
            ServiceLastUpdate = (uint)((bytes[pointer++] << 24) + (bytes[pointer++] << 16) + (bytes[pointer++] << 8) + bytes[pointer++]);
            StbIdLength = bytes[pointer++];
            StbIds = new StbId[StbIdLength];

            for (int i = 0; i < StbIds.Length; i++)
            {
                StbIds[i] = new StbId(bytes[pointer++]);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Service Version: {ServiceVersion}\n";
            str += $"{prefix}Service status: {ServiceStatus}\n";
            str += $"{prefix}Update type: {ServiceUpdateTypeStr}\n";
            str += $"{prefix}Service last update: {ServiceLastUpdateTime}\n";
            str += $"{prefix}Service id: {ServiceId}\n";
            str += $"{prefix}Onid: {OriginalNetworkId}\n";
            str += $"{prefix}Ts id: {TransportStreamId}\n";
            str += $"{prefix}For next stb IDs:\n";
            foreach (var stbId in StbIds)
            {
                str += stbId.Print(prefixLen + 2);
            }
            return str;
        }
        private string GetUpdateStr(byte bt)
        {
            switch (bt)
            {
                case 0: return "Auto update disabled, 0";
                case 1: return "Forced update in StandBy, 1";
                case 2: return "Forced update in normal mode, 2";
                case 3: return "Voluntary update in normal mode, 3";
                default: return "Incorrect update type";
            }
        }
    }
}
