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

internal sealed class EitFactory : SectionTableFactory<EIT, (byte TableId, ushort ServiceId, byte SectionNumber, byte LastSectionNumber)>
{
    public EitFactory()
        : base("EIT")
    {
    }

    internal event EitReady? OnEitReady;

    internal EIT? Eit => CurrentTable;

    protected override bool DropSameVersionForSameKey => true;

    protected override bool IsExpectedTableId(byte tableId)
    {
        return tableId is 0x4F or 0x4E || (0x50 <= tableId && tableId <= 0x5F) || (0x60 <= tableId && tableId <= 0x6F);
    }

    protected override EIT ParseTable(ReadOnlySpan<byte> bytes) => new(bytes);

    protected override (byte TableId, ushort ServiceId, byte SectionNumber, byte LastSectionNumber) GetSectionKey(EIT table)
    {
        return (table.TableId, table.ServiceId, table.SectionNumber, table.LastSectionNumber);
    }

    protected override string GetInvalidTableIdMessage(byte tableId) => $"Invalid table id: {tableId} for EIT table";

    protected override string? GetVersionChangedMessage(EIT previous, EIT current) => null;

    protected override void Publish(EIT table)
    {
        OnEitReady?.Invoke(table);
    }
}
