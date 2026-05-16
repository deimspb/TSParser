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

using NUnit.Framework;
using TSParser.Tests.Helpers;

namespace TSParser.Tests.Tables;

[TestFixture]
public sealed class TableSmokeTests
{
    public static IEnumerable<string> TableFixturePaths() => TableTestCatalog.EnumerateFixturePaths();

    [TestCaseSource(nameof(TableFixturePaths))]
    public void Parsed_table_matches_manifest_clr_type(string relativePath)
    {
        var entry = ManifestReader.Default.Tables.Tables[relativePath];
        var table = FixtureLoader.LoadTable(entry);

        Assert.That(table, Is.InstanceOf(TableTestCatalog.ResolveClrType(entry.ClrType)),
            () => $"{relativePath} ({entry.Type})");
        Assert.That(table.GetType().Name, Is.EqualTo(entry.ClrType));
    }

    [TestCaseSource(nameof(TableFixturePaths))]
    public void Section_tail_crc_matches_table_crc32(string relativePath)
    {
        var entry = ManifestReader.Default.Tables.Tables[relativePath];
        var bytes = FixtureLoader.LoadBytes(entry.RelativePath);
        var table = FixtureLoader.LoadTable(entry);

        Assert.AreEqual(FixtureLoader.Crc32FromSectionTail(bytes), table.CRC32,
            "Trailing section CRC32 should match parsed Table.CRC32");
    }

    [TestCaseSource(nameof(TableFixturePaths))]
    public void Reparse_compare_tables_has_no_differences(string relativePath)
    {
        var entry = ManifestReader.Default.Tables.Tables[relativePath];
        var bytes = FixtureLoader.LoadBytes(entry.RelativePath);
        var mip = TableTestCatalog.UsesMipLoader(entry.Type);
        var parser = new TsParser();

        var first = FixtureLoader.LoadTable(entry);
        var second = TsParser.GetOneTableFromBytes(bytes, mip);
        var differences = parser.CompareTables(first, second)
            .Where(d => !d.EndsWith(" equals", StringComparison.Ordinal))
            .ToList();

        Assert.That(differences, Is.Empty,
            () => $"CompareTables reported differences for {relativePath}:{Environment.NewLine}{string.Join(Environment.NewLine, differences)}");
    }
}
