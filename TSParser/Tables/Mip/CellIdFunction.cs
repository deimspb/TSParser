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

using System.Buffers.Binary;
using TSParser.Service;

namespace TSParser.Tables.Mip
{
    public record CellIdFunction : Function
    {
        public ushort CellId { get; }
        public bool WaitForEnableFlag { get; }
        public CellIdFunction(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            var pointer = 2;
            CellId = BinaryPrimitives.ReadUInt16BigEndian(bytes[pointer..]);
            pointer += 2;
            WaitForEnableFlag = (bytes[pointer] & 0x80) != 0;
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Tx Function: {FunctionName}, Cell id: {CellId}, wait for enable flag: {WaitForEnableFlag}\n";
        }
    }
}