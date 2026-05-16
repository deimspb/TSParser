#Requires -Version 5.1
<#
.SYNOPSIS
  Build and run TSParser.Benchmarks (BenchmarkDotNet) with JSON export.

.DESCRIPTION
  Runs fixture-based benchmarks by default (tables, descriptors, descriptor loops).
  Full TS file benchmarks use category RequiresTsFile; pass -IncludeFullTs when a corpus is available.

  Environment:
    TSPARSER_TEST_CORPUS     - override TestResources root
    TSPARSER_PERF_TS_MEDIUM  - medium .ts for ParseFullTsBenchmarks
    TSPARSER_PERF_TS_LARGE   - large .ts for ParseFullTsLargeBenchmarks
    TSPARSER_TS_ROOT         - fallback corpus directory (default D:\Dvb\dvb_lib)
    DOTNET_TC_QuickJitForLoop=0  - recommended for stable timings (see perf/README.md)

.EXAMPLE
  .\run-benchmarks.ps1
  .\run-benchmarks.ps1 -IncludeFullTs
  .\run-benchmarks.ps1 -Filter '*ParseTable*'
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $Filter = '',
    [switch] $IncludeFullTs,
    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Project = Join-Path $RepoRoot 'TSParser.Benchmarks\TSParser.Benchmarks.csproj'
$Artifacts = Join-Path $RepoRoot 'BenchmarkDotNet.Artifacts'

if (-not $SkipBuild) {
    dotnet build $Project -c $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$filterPatterns = if ([string]::IsNullOrWhiteSpace($Filter)) {
    if ($IncludeFullTs) {
        @('*ParseTable*', '*Descriptor*', '*ParseFullTs*')
    }
    else {
        @('*ParseTable*', '*ParseDescriptor*', '*DescriptorLoop*')
    }
}
else {
    @($Filter)
}

$args = @(
    'run', '--no-build',
    '-c', $Configuration,
    '--project', $Project,
    '-p:Platform=x64',
    '--',
    '--filter'
) + $filterPatterns

Push-Location $RepoRoot
try {
    & dotnet @args
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($code -ne 0) { exit $code }

Write-Host "BenchmarkDotNet artifacts: $Artifacts" -ForegroundColor Cyan
Write-Host "Latest run: perf\baselines\current.json" -ForegroundColor Cyan
Write-Host "Promote baseline: .\update-perf-baseline.ps1" -ForegroundColor Cyan
