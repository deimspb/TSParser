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

internal sealed class PatFactory : SectionTableFactory<PAT, byte>
{
    public PatFactory()
        : base("PAT")
    {
    }

    internal event PatReady? OnPatReady;

    internal PAT? Pat => CurrentTable;

    protected override bool IsExpectedTableId(byte tableId) => tableId == 0x00;

    protected override PAT ParseTable(ReadOnlySpan<byte> bytes) => new(bytes);

    protected override byte GetSectionKey(PAT table) => 0;

    protected override string GetInvalidTableIdMessage(byte tableId) => $"Invalid table id: {tableId} for PAT";

    protected override void Publish(PAT table)
    {
        OnPatReady?.Invoke(table);
    }
}
