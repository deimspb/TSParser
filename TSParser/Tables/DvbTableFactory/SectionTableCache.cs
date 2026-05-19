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
using TSParser.Tables;

namespace TSParser.Tables.DvbTableFactory;

internal sealed class SectionTableCache<TTable, TKey>
    where TTable : Table
    where TKey : notnull
{
    private readonly Dictionary<TKey, TTable> _sectionCache = new();
    private readonly HashSet<uint> _seenCrc = new();

    public bool HasCrc(uint crc32) => _seenCrc.Contains(crc32);

    public bool TryAccept(
        TTable table,
        TKey key,
        bool dropSameVersionForSameKey,
        Func<TTable, TTable, string?>? versionChangedMessage)
    {
        if (_sectionCache.TryGetValue(key, out var previous))
        {
            if (dropSameVersionForSameKey && previous.VersionNumber == table.VersionNumber)
            {
                _seenCrc.Add(table.CRC32);
                return false;
            }

            if (previous.VersionNumber != table.VersionNumber)
            {
                var message = versionChangedMessage?.Invoke(previous, table);
                if (message != null)
                {
                    Logger.Send(LogStatus.INFO, message);
                }
            }
        }

        _sectionCache[key] = table;
        _seenCrc.Add(table.CRC32);
        return true;
    }
}
