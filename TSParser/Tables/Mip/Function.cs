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

namespace TSParser.Tables.Mip
{
    public record Function
    {
        public byte FunctionTag { get; }
        public byte FunctionLength { get; }
        public string FunctionName => Dictionaries.GetTxFunctionName(FunctionTag);
        public Function(ReadOnlySpan<byte> bytes)
        {
            FunctionTag = bytes[0];
            FunctionLength = bytes[1];
        }
        public virtual string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}Tx Function: {FunctionName}, function length: {FunctionLength}\n";
        }
    }
}
