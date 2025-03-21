﻿// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com 
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

namespace TSParser.Descriptors.Dvb
{
    public record ServiceDescriptor_0x48 : Descriptor
    {
        public byte ServiceType { get; }
        public string ServiceTypeName => Dictionaries.GetServiceType(ServiceType);
        public byte ServiceProviderNameLength { get; }
        public string ServiceProviderName { get; }
        public byte ServiceNameLenght { get; }
        public string ServiceName { get; }
        public ServiceDescriptor_0x48(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ServiceType = bytes[pointer++];
            ServiceProviderNameLength = bytes[pointer++];
            ServiceProviderName = Dictionaries.BytesToString(bytes.Slice(pointer, ServiceProviderNameLength));
            pointer += ServiceProviderNameLength;
            ServiceNameLenght = bytes[pointer++];
            ServiceName = Dictionaries.BytesToString(bytes.Slice(pointer, ServiceNameLenght));
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Service type: {ServiceTypeName}, Service provider: {ServiceProviderName}, Service name: {ServiceName}\n";
        }
    }
}
