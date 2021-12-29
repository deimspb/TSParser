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

using System.Buffers.Binary;
using TSParser.Service;

namespace TSParser.Descriptors.AitDescriptors
{
    public record ApplicationStorageDescriptor_0x10 : AitDescriptor
    {
        public byte StorageProperty { get; }
        public bool NotLaunchableFromBroadcast { get; }
        public bool LaunchableCompletelyFromCache { get; }
        public bool IsLaunchableWithOlderVersion { get; }
        public uint Version { get; }
        public byte Priority { get; }
        public ApplicationStorageDescriptor_0x10(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            StorageProperty = bytes[pointer++];
            NotLaunchableFromBroadcast = (bytes[pointer] & 0x80) != 0;
            LaunchableCompletelyFromCache = (bytes[pointer] & 0x40) != 0;
            IsLaunchableWithOlderVersion = (bytes[pointer++] & 0x20) != 0;
            Version = BinaryPrimitives.ReadUInt32BigEndian(bytes[pointer..]) & 0x7FFF;
            pointer += 4;
            Priority = bytes[pointer];
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}AIT Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            str += $"{prefix}Storage Property: {GetStorageProperty(StorageProperty)}\n";
            str += $"{prefix}Not Launchable From Broadcast: {NotLaunchableFromBroadcast}\n";
            str += $"{prefix}Launchable Completely From Cache: {LaunchableCompletelyFromCache}\n";
            str += $"{prefix}Is Launchable With Older Version: {IsLaunchableWithOlderVersion}\n";
            str += $"{prefix}Version: 0x{Version:X}\n";
            str += $"{prefix}Priority: {Priority}\n";

            return str;
        }
        private string GetStorageProperty(byte bt)
        {
            switch (bt)
            {
                case 0x00: return "broadcast related";
                case 0x01: return "stand alone";
                default: return "reserved";
            }
        }
    }
}
