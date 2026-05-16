#Requires -Version 5.1
<#
.SYNOPSIS
  Compare perf/baselines/net10.0-windows-x64.json against the latest benchmark run.

.DESCRIPTION
  Reads schemaVersion 1 JSON from perf/baselines/.
  Default current run: perf/baselines/current.json (written by TSParser.Benchmarks PerfBaselineExporter).

  Exit codes:
    0 - within warn threshold
    1 - regression at or above FailPercent (default 10%)
    2 - missing inputs

.EXAMPLE
  .\run-benchmarks.ps1
  .\compare-perf.ps1
#>
[CmdletBinding()]
param(
    [string] $BaselinePath = '',
    [string] $CurrentPath = '',
    [double] $WarnPercent = 5,
    [double] $FailPercent = 10,
    [string] $RepoRoot = $(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Import-NormalizedBaseline {
    param([string] $Path)
    if (-not (Test-Path $Path)) {
        throw "Baseline not found: $Path"
    }
    $doc = Get-Content -Raw -Path $Path | ConvertFrom-Json
    if ($doc.schemaVersion -ne 1) {
        throw "Unsupported baseline schema in $Path"
    }
    return $doc
}

$baselineDir = Join-Path $RepoRoot 'perf\baselines'

if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = Join-Path $baselineDir 'net10.0-windows-x64.json'
}

if ([string]::IsNullOrWhiteSpace($CurrentPath)) {
    $CurrentPath = Join-Path $baselineDir 'current.json'
}

$baseline = Import-NormalizedBaseline -Path $BaselinePath
$current = Import-NormalizedBaseline -Path $CurrentPath

$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$missing = New-Object System.Collections.Generic.List[string]

foreach ($prop in $baseline.benchmarks.PSObject.Properties) {
    $name = $prop.Name
    $base = $prop.Value
    $curProp = $current.benchmarks.PSObject.Properties[$name]
    if (-not $curProp) {
        $missing.Add($name)
        continue
    }

    $cur = $curProp.Value
    if ($base.meanNs -le 0) { continue }

    $deltaPct = (($cur.meanNs - $base.meanNs) / $base.meanNs) * 100.0
    $allocNote = ''
    if ($null -ne $base.allocatedBytes -and $null -ne $cur.allocatedBytes -and $base.allocatedBytes -gt 0) {
        $allocPct = (($cur.allocatedBytes - $base.allocatedBytes) / $base.allocatedBytes) * 100.0
        $allocNote = ", alloc {0:N1}%" -f $allocPct
    }

    $line = "{0,-72} mean {1,6:N1}% ({2} ns -> {3} ns){4}" -f $name, $deltaPct, $base.meanNs, $cur.meanNs, $allocNote
    if ($deltaPct -ge $FailPercent) {
        $failures.Add($line)
    }
    elseif ($deltaPct -ge $WarnPercent) {
        $warnings.Add($line)
    }
    else {
        Write-Host $line
    }
}

Write-Host ""
Write-Host "Baseline: $BaselinePath" -ForegroundColor DarkGray
Write-Host "Current:  $CurrentPath" -ForegroundColor DarkGray

if ($missing.Count -gt 0) {
    Write-Warning "Missing in current run ($($missing.Count)): $($missing[0..([Math]::Min(4, $missing.Count - 1))] -join ', ')$(if ($missing.Count -gt 5) { '...' })"
}

foreach ($w in $warnings) {
    Write-Host "WARN  $w" -ForegroundColor Yellow
}

foreach ($f in $failures) {
    Write-Host "FAIL  $f" -ForegroundColor Red
}

if ($failures.Count -gt 0) {
    Write-Host "`nRegression at or above $FailPercent% on $($failures.Count) benchmark(s)." -ForegroundColor Red
    exit 1
}

if ($warnings.Count -gt 0) {
    Write-Host "`nWarnings at or above $WarnPercent% on $($warnings.Count) benchmark(s)." -ForegroundColor Yellow
}

exit 0
