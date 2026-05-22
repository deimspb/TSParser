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

internal sealed class EwsFactory : SectionTableFactory<EWS, byte>
{
    public EwsFactory()
        : base("EWS")
    {
    }

    internal event EwsReady? OnEwsReady;

    internal EWS? Ews => CurrentTable;

    protected override bool IsExpectedTableId(byte tableId) => tableId == 0x93;

    protected override bool IsTableIdHandledBySiblingFactory(byte tableId) => tableId is 0x94 or 0x95;

    protected override EWS ParseTable(ReadOnlySpan<byte> bytes) => new(bytes, CurrentPid);

    protected override byte GetSectionKey(EWS table) => 0;

    protected override string GetInvalidTableIdMessage(byte tableId) => $"Invalid table id: {tableId} for EWS table";

    protected override string GetCrcErrorMessage() => $"EWS pid {CurrentPid} CRC incorrect!";

    protected override string? GetVersionChangedMessage(EWS previous, EWS current)
    {
        return $"EWS table updated to version {current.VersionNumber} for PID: 0x{CurrentPid:X}";
    }

    protected override void Publish(EWS table)
    {
        OnEwsReady?.Invoke(table);
    }
}
