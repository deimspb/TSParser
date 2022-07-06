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

using TSParser.Service;

namespace TSParser.Tables.Scte35
{
    public class EncryptedPacket
    {
        public byte EncryptionAlgorithm { get; }
        public byte CwIndex { get; }
        public string EncryptionAlgotithmName => GetEncryptionAlgo(EncryptionAlgorithm);
        public EncryptedPacket(ReadOnlySpan<byte> bytes)
        {
            var pointer = 0;
            EncryptionAlgorithm = (byte)((bytes[pointer] & 0x7E) >> 1);
            pointer += 5;
            CwIndex = bytes[pointer];
        }

        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string prefix = Utils.Prefix(prefixLen);

            string str = $"{headerPrefix}Encrypted Packet\n";
            str += $"{prefix}Encryption Algorithm: {EncryptionAlgotithmName}\n";
            str += $"{prefix}Cw index: {CwIndex}\n";
            return str;
        }

        private static string GetEncryptionAlgo(byte bt)
        {
            return bt switch
            {
                0 => "No encryption",
                1 => "DES ECB mode",
                2 => "DES CBC mode",
                3 => "Triple DES EDE3-ECB mode",
                byte n when n >= 4 && n <= 31 => "Reserved",
                _ => "User private",
            };
        }
    }
}
