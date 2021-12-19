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
    public record ContentDescriptor_0x54:Descriptor
    {
        public ContentNibble[] ContentNibbles { get; } //TODO: implement content nibble descriptions + to string
        public ContentDescriptor_0x54(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            ContentNibbles = new ContentNibble[DescriptorLength / 2];
            for (int i = 0; i < ContentNibbles.Length; i++)
            {
                ContentNibbles[i] = new ContentNibble(bytes.Slice(i * 2, 2));
            }
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
    public struct ContentNibble
    {
        public byte ContentNibble1 { get; }
        public byte ContentNibble2 { get; }
        public byte UserByte { get; }

        public ContentNibble(ReadOnlySpan<byte> bytes)
        {
            ContentNibble1 = (byte)(bytes[0] >> 4);
            ContentNibble2 = (byte)(bytes[0] & 0x0F);
            UserByte = bytes[1];
        }
    }
}
