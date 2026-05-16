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
    public record WhiteListDescriptor_0xC0 : Descriptor
    {
        public byte ServiceNameLength { get; }
        public string ServiceName { get; }
        public byte STB_Id_length { get; }
        public StbId[] StbIds { get; }
        public WhiteListDescriptor_0xC0(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ServiceNameLength = bytes[pointer++];
            ServiceName = Dictionaries.BytesToString(bytes.Slice(pointer, ServiceNameLength));
            pointer += ServiceNameLength;
            STB_Id_length = bytes[pointer++];
            StbIds = new StbId[STB_Id_length];

            for (int i = 0; i < STB_Id_length; i++)
            {
                StbIds[i] = new StbId(bytes[pointer++]);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Service name: {ServiceName}\n";
            str += $"{prefix}Allowed stb id:\n";
            foreach (var stbId in StbIds)
            {
                str += stbId.Print(prefixLen + 2);
            }
            return str;
        }
    }
}
