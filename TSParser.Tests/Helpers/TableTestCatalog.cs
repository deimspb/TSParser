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

using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;
using TSParser.Tables.Scte35;

namespace TSParser.Tests.Helpers;

/// <summary>
/// Supported SI table types for manifest-driven fixture tests (14 types, up to 4 samples each).
/// </summary>
public static class TableTestCatalog
{
    public const int TargetSamplesPerType = 4;

    public static readonly string[] SampleLabels = ["S", "M1", "M2", "L"];

    public static readonly string[] SupportedTypes =
    [
        "PAT", "CAT", "PMT", "NIT", "SDT", "BAT", "EIT", "TDT", "TOT", "AIT", "MIP", "SCTE35", "EWS", "EEWS",
    ];

    public static IEnumerable<string> EnumerateFixturePaths() =>
        ManifestReader.Default.EnumerateTableFixtures().Select(e => e.RelativePath);

    public static Type ResolveClrType(string clrTypeName) => clrTypeName switch
    {
        nameof(PAT) => typeof(PAT),
        nameof(CAT) => typeof(CAT),
        nameof(PMT) => typeof(PMT),
        nameof(NIT) => typeof(NIT),
        nameof(SDT) => typeof(SDT),
        nameof(BAT) => typeof(BAT),
        nameof(EIT) => typeof(EIT),
        nameof(TDT) => typeof(TDT),
        nameof(TOT) => typeof(TOT),
        nameof(AIT) => typeof(AIT),
        nameof(MIP) => typeof(MIP),
        nameof(SCTE35) => typeof(SCTE35),
        nameof(EWS) => typeof(EWS),
        nameof(EEWS) => typeof(EEWS),
        _ => throw new ArgumentException($"Unknown table CLR type: {clrTypeName}", nameof(clrTypeName)),
    };

    public static bool UsesMipLoader(string tableType) =>
        tableType.Equals("MIP", StringComparison.OrdinalIgnoreCase);
}
