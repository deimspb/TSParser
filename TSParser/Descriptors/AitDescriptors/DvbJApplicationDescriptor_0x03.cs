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

using System.Collections.Generic;
using TSParser.Service;

namespace TSParser.Descriptors.AitDescriptors
{
    
    public record DvbJApplicationDescriptor_0x03 : AitDescriptor
    {
        public List<Parameter> Parameters { get; }
        public DvbJApplicationDescriptor_0x03(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            Parameters = new List<Parameter>();
            while(pointer<DescriptorLength - 2)
            {
                var param = new Parameter(bytes[pointer..]);
                pointer += param.ParameterLength + 1;
                Parameters.Add(param);
            }
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}AIT Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Parameters count: {Parameters.Count}\n";
            if(Parameters.Count > 0)
            {
                foreach(var param in Parameters)
                {
                    str+=param.Print(prefixLen + 4);
                }
            }
            return str;
        }
    }
    public struct Parameter
    {
        public byte ParameterLength { get; }
        public byte[] ParameterBytes { get; }
        public Parameter(ReadOnlySpan<byte> bytes)
        {
            ParameterLength = bytes[0];
            ParameterBytes = new byte[ParameterLength];
            bytes.Slice(0,ParameterLength).CopyTo(ParameterBytes);
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Parameter bytes: {BitConverter.ToString(ParameterBytes):X}\n";
        }
    }
}
