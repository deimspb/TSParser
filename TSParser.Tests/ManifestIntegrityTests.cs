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

namespace TSParser.Tests;

[TestFixture]
public sealed class ManifestIntegrityTests
{
    [Test]
    public void Tables_manifest_entries_reference_existing_files()
    {
        var missing = ManifestReader.Default.Tables.Tables.Values
            .Where(e => !File.Exists(FixtureLoader.ResolvePath(e.RelativePath)))
            .Select(e => e.RelativePath)
            .ToList();

        Assert.That(missing, Is.Empty,
            () => $"manifest.tables.json lists fixtures that are not on disk:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Test]
    public void Descriptors_manifest_entries_reference_existing_files()
    {
        var missing = ManifestReader.Default.Descriptors.Descriptors.Values
            .Where(e => !File.Exists(FixtureLoader.ResolvePath(e.RelativePath)))
            .Select(e => e.RelativePath)
            .ToList();

        Assert.That(missing, Is.Empty,
            () => $"manifest.descriptors.json lists fixtures that are not on disk:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Test]
    public void At_least_one_table_fixture_is_available()
    {
        Assert.That(ManifestReader.Default.EnumerateTableFixtures().Any(), Is.True,
            "Add .tbl fixtures under TestResources/Tables and run tools/bless-manifest.ps1");
    }
}
