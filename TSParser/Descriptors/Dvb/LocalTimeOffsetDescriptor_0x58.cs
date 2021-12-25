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
    public record LocalTimeOffsetDescriptor_0x58 : Descriptor
    {
        public LocalTime[] LocalTimes { get; }

        public LocalTimeOffsetDescriptor_0x58(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            LocalTimes = new LocalTime[DescriptorLength/13];
            for (int i = 0; i < LocalTimes.Length; i++)
            {
                LocalTimes[i] = new LocalTime(bytes.Slice(2+i*13));
            }
        }
        public override string ToString()
        {
            string str = $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var i in LocalTimes)
            {
                str += $"            {i}\n";
            }
            return str;
        }
        public override string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);

            string str = $"{header}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var i in LocalTimes)
            {
                str += i.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct LocalTime
    {
        public string CountryCode { get; }
        public byte CountryRegionId { get; }
        public bool LocalTimeOffsetPolarity { get; }
        public TimeSpan LocalTimeOffset { get; }  
        public DateTime TimeOfChange { get; }
        public TimeSpan NextTimeOffset { get; }
        public LocalTime(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            CountryCode = Dictionaries.BytesToString(bytes.Slice(pointer, 3));
            pointer += 3;
            CountryRegionId = (byte)((bytes[pointer] & 0xFC) >> 2);
            //reserved 1 bit
            LocalTimeOffsetPolarity = (bytes[pointer++] & 0x01) != 0;
            LocalTimeOffset = Utils.GetTimeOffset(bytes.Slice(pointer, 2));
            pointer += 2;
            TimeOfChange = Utils.GetDateTimeFromMJD_UTC(bytes.Slice(pointer, 5));
            pointer += 5;
            NextTimeOffset = Utils.GetTimeOffset(bytes.Slice(pointer, 2));
        }
        public override string ToString()
        {
            return $"Country: {CountryCode}, region id: {CountryRegionId}, polarity: {LocalTimeOffsetPolarity}, time offset: {LocalTimeOffset}, time of change: {TimeOfChange}, next time offset: {NextTimeOffset}";
        }
        public string Print(int prefixLen)
        {
            string header = Utils.HeaderPrefix(prefixLen);
            return $"{header}Country: {CountryCode}, region id: {CountryRegionId}, polarity: {LocalTimeOffsetPolarity}, time offset: {LocalTimeOffset}, time of change: {TimeOfChange}, next time offset: {NextTimeOffset}\n";
        }
    }
}
