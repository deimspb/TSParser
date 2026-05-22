#Requires -Version 5.1
<#
.SYNOPSIS
  Full transport-stream SI smoke: table counters and ETSI/EXCEPTION logs.

.DESCRIPTION
  Runs TSParser.Benchmarks in --smoke mode (table decode, same as ParseFullTs_AllTables).
  By default parses medium + large files from TSPARSER_PERF_TS_MEDIUM / TSPARSER_PERF_TS_LARGE
  or corpus under TSPARSER_TS_ROOT (default D:\Dvb\dvb_lib).

.EXAMPLE
  $env:TSPARSER_PERF_TS_MEDIUM = 'D:\Dvb\dvb_lib\9.ts'
  $env:TSPARSER_PERF_TS_LARGE  = 'D:\Dvb\dvb_lib\27_5min.ts'
  .\run-full-ts-smoke.ps1

.EXAMPLE
  .\run-full-ts-smoke.ps1 -TsPath 'D:\Dvb\dvb_lib\9.ts'
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string[]] $TsPath = @(),
    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Project = Join-Path $RepoRoot 'TSParser.Benchmarks\TSParser.Benchmarks.csproj'

if (-not $SkipBuild) {
    dotnet build $Project -c $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$args = @(
    'run', '--no-build',
    '-c', $Configuration,
    '--project', $Project,
    '-p:Platform=x64',
    '--',
    '--smoke'
) + $TsPath

Push-Location $RepoRoot
try {
    & dotnet @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
