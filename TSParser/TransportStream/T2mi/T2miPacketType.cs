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

namespace TSParser.TransportStream.T2mi;

/// <summary>T2-MI packet types (ETSI TS 102 773).</summary>
public enum T2miPacketType : byte
{
    BasebandFrame = 0x00,
    AuxiliaryStreamIqData = 0x01,
    ArbitraryCellInsertion = 0x02,
    L1Current = 0x10,
    L1Future = 0x11,
    P2BiasBalancingCells = 0x12,
    DvbT2Timestamp = 0x20,
    IndividualAddressing = 0x21,
    FefPartNull = 0x30,
    FefPartIqData = 0x31,
    FefPartComposite = 0x32,
    FefSubPart = 0x33,
}
