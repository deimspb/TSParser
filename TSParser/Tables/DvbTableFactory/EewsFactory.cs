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

internal sealed class EewsFactory : SectionTableFactory<EEWS, byte>
{
    public EewsFactory()
        : base("EEWS")
    {
    }

    internal event EewsReady? OnEewsReady;

    internal EEWS? Eews => CurrentTable;

    protected override bool IsExpectedTableId(byte tableId) => tableId is 0x94 or 0x95;

    protected override EEWS ParseTable(ReadOnlySpan<byte> bytes) => new(bytes, CurrentPid);

    protected override byte GetSectionKey(EEWS table) => 0;

    protected override string GetInvalidTableIdMessage(byte tableId) => $"Invalid table id: {tableId} for EEWS table";

    protected override string GetCrcErrorMessage() => $"EEWS pid {CurrentPid} CRC incorrect!";

    protected override void Publish(EEWS table)
    {
        OnEewsReady?.Invoke(table);
    }
}
