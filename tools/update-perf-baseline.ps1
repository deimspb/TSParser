#Requires -Version 5.1
<#
.SYNOPSIS
  Promote perf/baselines/current.json to the canonical baseline file.

.DESCRIPTION
  After .\run-benchmarks.ps1, BenchmarkDotNet writes perf/baselines/current.json via PerfBaselineExporter.
  This script copies it to perf/baselines/net10.0-windows-x64.json (or -BaselineName) for git commit.

.EXAMPLE
  .\run-benchmarks.ps1
  .\update-perf-baseline.ps1
#>
[CmdletBinding()]
param(
    [string] $SourcePath = '',
    [string] $BaselineName = 'net10.0-windows-x64.json',
    [string] $RepoRoot = $(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$outDir = Join-Path $RepoRoot 'perf\baselines'
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $outDir 'current.json'
}

if (-not (Test-Path $SourcePath)) {
    throw "No current run at $SourcePath. Run .\run-benchmarks.ps1 first."
}

$dest = Join-Path $outDir $BaselineName
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Copy-Item -Force $SourcePath $dest

$doc = Get-Content -Raw -Path $dest | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($doc.sdk)) {
    $doc | Add-Member -NotePropertyName sdk -NotePropertyValue (dotnet --version) -Force
    $doc | ConvertTo-Json -Depth 6 | Set-Content -Path $dest -Encoding utf8
}
$count = @($doc.benchmarks.PSObject.Properties).Count
Write-Host "Promoted baseline ($count benchmarks): $dest" -ForegroundColor Green
