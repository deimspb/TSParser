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

using System.Buffers.Binary;
using TSParser.Service;
using TSParser.Tables;
using TSParser.TransportStream;

namespace TSParser.Tables.DvbTableFactory;

internal abstract class SectionTableFactory<TTable, TKey> : TableFactory
    where TTable : Table
    where TKey : notnull
{
    private readonly SectionTableCache<TTable, TKey> _sectionCache = new();

    protected SectionTableFactory(string tableName)
    {
        TableName = tableName;
    }

    protected string TableName { get; }

    protected TTable? CurrentTable { get; private set; }

    protected virtual bool DropSameVersionForSameKey => false;

    protected override void OnResetStreamState()
    {
        _sectionCache.Clear();
        CurrentTable = null;
    }

    internal override void PushTable(TsPacket tsPacket)
    {
        ProcessAssembledSections(tsPacket);
    }

    protected sealed override void ProcessCurrentSection()
    {
        ReadOnlySpan<byte> bytes = TableData.AsSpan();

        if (!IsExpectedTableId(bytes[0]))
        {
            var tableId = bytes[0];
            if (!IsTableIdHandledBySiblingFactory(tableId))
                Logger.Send(LogStatus.ETSI, GetInvalidTableIdMessage(tableId));

            return;
        }

        var crc32 = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
        if (_sectionCache.HasCrc(crc32))
        {
            return;
        }

        if (Utils.GetCRC32(bytes[..^4]) != crc32)
        {
            Logger.Send(LogStatus.ETSI, GetCrcErrorMessage());
            ResetFactory();
            return;
        }

        if (!TryParseAssembledTable(() => ParseAndPublish(crc32), TableName))
        {
            return;
        }
    }

    protected abstract bool IsExpectedTableId(byte tableId);

    /// <summary>
    /// When the same PID carries EWS (0x93) and EEWS (0x94/0x95), the sibling factory owns these table IDs.
    /// </summary>
    protected virtual bool IsTableIdHandledBySiblingFactory(byte tableId) => false;

    protected abstract TTable ParseTable(ReadOnlySpan<byte> bytes);

    protected abstract TKey GetSectionKey(TTable table);

    protected abstract void Publish(TTable table);

    protected virtual string GetInvalidTableIdMessage(byte tableId)
    {
        return $"Invalid table id: {tableId} for {TableName} table";
    }

    protected virtual string GetCrcErrorMessage()
    {
        return $"{TableName} CRC incorrect!";
    }

    protected virtual string? GetVersionChangedMessage(TTable previous, TTable current)
    {
        return $"{TableName} version changed from {previous.VersionNumber} to {current.VersionNumber}";
    }

    private void ParseAndPublish(uint crc32)
    {
        var parsed = ParseTable(TableData);
        var key = GetSectionKey(parsed);

        if (_sectionCache.TryAccept(parsed, key, DropSameVersionForSameKey, GetVersionChangedMessage))
        {
            CurrentTable = parsed;
            Publish(parsed);
        }
    }
}
