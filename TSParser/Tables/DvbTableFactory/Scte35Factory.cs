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

internal sealed class Scte35Factory : SectionTableFactory<SCTE35, byte>
{
    public Scte35Factory()
        : base("SCTE-35")
    {
    }

    internal event Scte35Ready? OnScte35Ready;

    internal SCTE35? Scte35 => CurrentTable;

    protected override bool IsExpectedTableId(byte tableId) => tableId == 0xFC;

    protected override SCTE35 ParseTable(ReadOnlySpan<byte> bytes) => new(bytes, CurrentPid);

    protected override byte GetSectionKey(SCTE35 table) => 0;

    protected override string GetCrcErrorMessage() => $"SCTE35 pid {CurrentPid} CRC incorrect!";

    protected override void Publish(SCTE35 table)
    {
        OnScte35Ready?.Invoke(table);
    }
}
