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
public sealed class TableFixtureTests
{
    public static IEnumerable<string> TableFixturePaths() =>
        ManifestReader.Default.EnumerateTableFixtures().Select(e => e.RelativePath);

    [TestCaseSource(nameof(TableFixturePaths))]
    public void Parse_table_sample(string relativePath)
    {
        var entry = ManifestReader.Default.Tables.Tables[relativePath];
        var table = FixtureLoader.LoadTable(entry);
        ExpectedAssert.AssertTable(table, entry);
    }
}
