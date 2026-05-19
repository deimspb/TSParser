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

using TSParser.Tables.DvbTables;

namespace TSParser.Tables.DvbTableFactory;

internal sealed class NitFactory : SectionTableFactory<NIT, (ushort NetworkId, byte SectionNumber, byte LastSectionNumber)>
{
    public NitFactory()
        : base("NIT")
    {
    }

    internal event NitReady? OnNitReady;

    internal NIT? Nit => CurrentTable;

    protected override bool DropSameVersionForSameKey => true;

    protected override bool IsExpectedTableId(byte tableId) => tableId is 0x40 or 0x41;

    protected override NIT ParseTable(ReadOnlySpan<byte> bytes) => new(bytes);

    protected override (ushort NetworkId, byte SectionNumber, byte LastSectionNumber) GetSectionKey(NIT table)
    {
        return (table.NetworkId, table.SectionNumber, table.LastSectionNumber);
    }

    protected override string GetInvalidTableIdMessage(byte tableId) => $"Not implement table id: {tableId} for NIT";

    protected override string? GetVersionChangedMessage(NIT previous, NIT current)
    {
        return $"NIT table version changed for ts id: {previous.NetworkId} from {previous.VersionNumber} to {current.VersionNumber}";
    }

    protected override void Publish(NIT table)
    {
        OnNitReady?.Invoke(table);
    }
}
