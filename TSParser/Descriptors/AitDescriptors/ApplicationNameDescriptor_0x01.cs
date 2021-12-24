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

namespace TSParser.Descriptors.AitDescriptors
{
    public record ApplicationNameDescriptor_0x01 : AitDescriptor
    {
        public List<ApplicationName> ApplicationNames { get; } = null!;
        public ApplicationNameDescriptor_0x01(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            ApplicationNames = new List<ApplicationName>();

            while (pointer < DescriptorLength)
            {
                var item = new ApplicationName(bytes[pointer..]);
                pointer += item.ApplicationNameLength + 3;
                ApplicationNames.Add(item);
            }
        }
        public override string ToString()
        {
            string str = $"         AIT descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var applicationName in ApplicationNames)
            {
                str += $"                  {applicationName}\n";
            }
            return str;
        }
    }
    public struct ApplicationName
    {
        public string Iso639LanguageCode { get; }
        public byte ApplicationNameLength { get; }
        public string ApplicationNameChar { get; }
        public ApplicationName(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            Iso639LanguageCode = Dictionaries.BytesToString(bytes.Slice(pointer, 3));
            pointer += 3;
            ApplicationNameLength = bytes[pointer++];
            ApplicationNameChar = Dictionaries.BytesToString(bytes.Slice(pointer, ApplicationNameLength));
        }
        public override string ToString()
        {
            return $"Language code: {Iso639LanguageCode}, Application name: {ApplicationNameChar}";
        }
    }

}
