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
    public record ExtendedEventDescriptor_0x4E : Descriptor
    {
        public byte DescriptorNumber { get; }
        public byte LastDescriptorNumber { get; }
        public string Iso639 { get; }
        public byte LengthOfItems { get; }
        public List<EventItem> EventItems { get; } = null!;
        public byte TextLength { get; }
        public string Text { get; }
        public ExtendedEventDescriptor_0x4E(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            DescriptorNumber = (byte)(bytes[pointer] >> 4);
            LastDescriptorNumber = (byte)(bytes[pointer++] & 0x0F);
            Iso639 = Dictionaries.BytesToString(bytes.Slice(pointer, 3));
            pointer += 3;
            LengthOfItems = bytes[pointer++];
            if (LengthOfItems > 1)
                EventItems = GetEventList(bytes.Slice(pointer, LengthOfItems));
            pointer += LengthOfItems;
            TextLength = bytes[pointer++];
            Text = Dictionaries.BytesToString(bytes.Slice(pointer, TextLength));
            //pointer += TextLength;

        }

        private List<EventItem> GetEventList(ReadOnlySpan<byte> bytes)
        {
            List<EventItem> items = new();
            var pointer = 0;
            while (pointer < bytes.Length)
            {
                EventItem item = new(bytes[pointer..]);
                pointer += item.ItemDescriptionLength + item.ItemLength + 2;
                items.Add(item);
            }
            return items;
        }

        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            if (EventItems?.Count > 0)
            {
                foreach (var item in EventItems)
                {
                    str += item.Print(prefixLen + 4);
                }
            }
            str += $"{prefix}{Text}\n";
            return str;
        }
    }

    public struct EventItem
    {
        public byte ItemDescriptionLength { get; }
        public string ItemDescription { get; }
        public byte ItemLength { get; }
        public string ItemText { get; }
        public EventItem(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            ItemDescriptionLength = bytes[pointer++];

            ItemDescription = Dictionaries.BytesToString(bytes.Slice(pointer, ItemDescriptionLength));
            pointer += ItemDescriptionLength;
            ItemLength = bytes[pointer++];
            ItemText = Dictionaries.BytesToString(bytes.Slice(pointer, ItemLength));

        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}{ItemDescription}, {ItemText}\n";
        }
    }
}
