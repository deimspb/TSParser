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

/// <summary>
/// Manifest-level coverage: every supported type is declared; non-missing types have on-disk fixtures.
/// </summary>
[TestFixture]
public sealed class TableCoverageTests
{
    public static IEnumerable<string> SupportedTableTypes() => TableTestCatalog.SupportedTypes;

    [TestCaseSource(nameof(SupportedTableTypes))]
    public void Manifest_declares_table_type_with_valid_sample_count(string tableType)
    {
        var types = ManifestReader.Default.Tables.Types;
        Assert.That(types, Contains.Key(tableType));

        var stats = types[tableType];
        Assert.That(stats.Samples, Is.InRange(0, TableTestCatalog.TargetSamplesPerType), $"{tableType} samples");

        if (stats.Missing)
        {
            Assert.That(stats.Samples, Is.Zero, $"{tableType} is missing in corpus but manifest lists samples");
            Assert.That(stats.Complete, Is.False, $"{tableType} is missing but marked complete");
            return;
        }

        if (stats.Complete)
        {
            Assert.That(stats.Samples, Is.EqualTo(TableTestCatalog.TargetSamplesPerType),
                $"{tableType} marked complete without {TableTestCatalog.TargetSamplesPerType} samples");
        }
    }

    [TestCaseSource(nameof(SupportedTableTypes))]
    public void On_disk_fixtures_exist_for_each_manifest_sample(string tableType)
    {
        var stats = ManifestReader.Default.Tables.Types[tableType];
        var fixtures = ManifestReader.Default.EnumerateTableFixtures()
            .Where(e => e.Type.Equals(tableType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.That(fixtures, Has.Count.EqualTo(stats.Samples),
            () => $"{tableType}: manifest samples={stats.Samples}, fixtures on disk={fixtures.Count}");

        if (!stats.Complete || stats.Samples < TableTestCatalog.TargetSamplesPerType)
        {
            return;
        }

        Assert.That(
            fixtures.Select(f => f.Sample).Where(s => s is not null),
            Is.EquivalentTo(TableTestCatalog.SampleLabels),
            $"{tableType} should expose S/M1/M2/L samples when complete");
    }

    [Test]
    public void Parametric_fixture_tests_cover_every_manifest_sample()
    {
        var manifestSamples = ManifestReader.Default.Tables.Types.Values.Sum(s => s.Samples);
        var fixturePaths = TableTestCatalog.EnumerateFixturePaths().ToList();

        Assert.That(fixturePaths, Has.Count.EqualTo(manifestSamples),
            "TableFixtureTests/TableSmokeTests TestCase count should equal manifest sample total");

        var typesWithFixtures = fixturePaths
            .Select(p => ManifestReader.Default.Tables.Tables[p].Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expectedTypes = ManifestReader.Default.Tables.Types
            .Where(kv => kv.Value.Samples > 0)
            .Select(kv => kv.Key)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(typesWithFixtures, Is.EquivalentTo(expectedTypes),
            "Each type with manifest samples must have at least one parametrized fixture test");
    }

    [Test]
    public void Full_corpus_target_is_fourteen_types_times_four_samples()
    {
        const int fullCorpusTestCases = 14 * TableTestCatalog.TargetSamplesPerType;
        var availableSamples = ManifestReader.Default.Tables.Types.Values.Sum(s => s.Samples);
        var missingTypes = ManifestReader.Default.Tables.Types
            .Where(kv => kv.Value.Missing)
            .Select(kv => kv.Key)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(availableSamples, Is.LessThanOrEqualTo(fullCorpusTestCases));
        Assert.That(TableTestCatalog.SupportedTypes, Has.Length.EqualTo(14));

        TestContext.WriteLine(
            $"Table parse/smoke TestCases: {availableSamples} (target {fullCorpusTestCases} when corpus supplies all types).");
        if (missingTypes.Count > 0)
        {
            TestContext.WriteLine($"Missing in corpus: {string.Join(", ", missingTypes)}");
        }
    }
}
