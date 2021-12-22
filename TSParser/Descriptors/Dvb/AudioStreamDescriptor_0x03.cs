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

namespace TSParser.Descriptors.Dvb
{
    public record AudioStreamDescriptor_0x03 : Descriptor
    {
        public bool FreeFormatFlag { get; }
        public bool Id { get; }
        public byte Layer { get; }
        public bool VariableRateAudioIndicator { get; }
        public AudioStreamDescriptor_0x03(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            FreeFormatFlag = (bytes[pointer] & 0x80) != 0;
            Id = (bytes[pointer] & 0x40) != 0;
            Layer = (byte)((bytes[pointer]&0x30)>> 4);
            VariableRateAudioIndicator = (bytes[pointer] & 0x8) != 0;
        }
        public override string ToString()
        {
            return $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}, layer: {Layer}";
        }
    }
}
