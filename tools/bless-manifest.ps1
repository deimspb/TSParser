#Requires -Version 5.1
<#
.SYNOPSIS
  Parse TestResources fixtures with TSParser and write blessed manifest JSON for unit tests.

.DESCRIPTION
  Builds tools/BlessManifest and updates:
    TSParser.Tests/TestResources/manifest.tables.json
    TSParser.Tests/TestResources/manifest.descriptors.json

  Run after select-samples.ps1 (tables) or CorpusHarvester descriptor selection so
  manifests contain CRC32, section lengths, and type-specific expected fields.

  Preserves metadata from an existing manifest (sourceTs, sourceStagingPath, generatedAt).

  Environment overrides:
    TSPARSER_TEST_FIXTURES - fixtures root (default: repo TSParser.Tests/TestResources)

.EXAMPLE
  .\bless-manifest.ps1
  .\bless-manifest.ps1 -FixturesRoot D:\Dvb\test_corpus -Configuration Release
  .\bless-manifest.ps1 -TablesOnly
#>
[CmdletBinding()]
param(
    [string] $FixturesRoot = $(if ($env:TSPARSER_TEST_FIXTURES) { $env:TSPARSER_TEST_FIXTURES } else { '' }),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $TablesOnly,
    [switch] $DescriptorsOnly,
    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $FixturesRoot) {
    $FixturesRoot = Join-Path $RepoRoot 'TSParser.Tests\TestResources'
}

$ProjectPath = Join-Path $RepoRoot 'tools\BlessManifest\BlessManifest.csproj'
$BlessArgs = @('--fixtures-root', (Resolve-Path -LiteralPath $FixturesRoot).Path)

if ($TablesOnly) { $BlessArgs += '--tables-only' }
if ($DescriptorsOnly) { $BlessArgs += '--descriptors-only' }

Write-Host "Fixtures: $FixturesRoot"
Write-Host "Config:   $Configuration"

if (-not $SkipBuild) {
    Write-Host 'Building BlessManifest...'
    dotnet build $ProjectPath -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host 'Blessing manifests...'
dotnet run --project $ProjectPath -c $Configuration --no-build -- @BlessArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host 'Bless complete.'
Write-Host "  $(Join-Path $FixturesRoot 'manifest.tables.json')"
Write-Host "  $(Join-Path $FixturesRoot 'manifest.descriptors.json')"

exit 0
