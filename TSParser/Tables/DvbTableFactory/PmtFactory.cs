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

internal sealed class PmtFactory : SectionTableFactory<PMT, byte>
{
    public PmtFactory()
        : base("PMT")
    {
    }

    internal event PmtReady? OnPmtReady;

    internal PMT? Pmt => CurrentTable;

    protected override bool IsExpectedTableId(byte tableId) => tableId == 0x02;

    protected override PMT ParseTable(ReadOnlySpan<byte> bytes) => new(bytes, CurrentPid);

    protected override byte GetSectionKey(PMT table) => 0;

    protected override string GetInvalidTableIdMessage(byte tableId) => $"Invalid table id: {tableId} for PMT table";

    protected override string GetCrcErrorMessage() => $"PMT pid {CurrentPid} CRC incorrect!";

    protected override void Publish(PMT table)
    {
        OnPmtReady?.Invoke(table);
    }
}
