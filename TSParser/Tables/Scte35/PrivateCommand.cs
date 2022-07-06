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

namespace TSParser.Tables.Scte35
{
    public record PrivateCommand : SpliceCommand
    {
        public byte[] PrivateBytes { get; }
        public uint Identifier { get; }
        public PrivateCommand(ReadOnlySpan<byte> bytes, byte spliceType) : base(bytes, spliceType)
        {
            var pointer = 0;
            Identifier = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            pointer += 4;
            PrivateBytes = new byte[bytes.Length - pointer];
            bytes[pointer..].CopyTo(PrivateBytes);
            SpliceCommandLength = (ushort)bytes.Length;
        }

        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Private command. identifier: {Identifier}\n";

        }
    }
}
