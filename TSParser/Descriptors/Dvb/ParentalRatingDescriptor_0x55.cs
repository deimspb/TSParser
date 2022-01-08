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

namespace TSParser.Descriptors.Dvb
{
    public record ParentalRatingDescriptor_0x55 : Descriptor
    {
        public ParentalRationgItem[] ParentalRatingItems { get; }
        public ParentalRatingDescriptor_0x55(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            ParentalRatingItems = new ParentalRationgItem[DescriptorLength / 4];
            for (int i = 0; i < ParentalRatingItems.Length; i++)
            {
                ParentalRatingItems[i] = new ParentalRationgItem(bytes.Slice(2 + i * 4, 4));
            }
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            string str = $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var item in ParentalRatingItems)
            {
                str += item.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct ParentalRationgItem
    {
        public string Country { get; }
        public byte Rating { get; }
        public string RatingAge => $"Minimum age: {Rating + 3}";
        public ParentalRationgItem(ReadOnlySpan<byte> bytes)
        {
            Country = Dictionaries.BytesToString(bytes[..3]);
            Rating = bytes[3];
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            
            return $"{header}{Country}, {RatingAge}\n";
        }
    }

}
