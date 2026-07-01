# CPU sample for ParseFullTs_AllTables on medium TS (default 9.ts via TSPARSER_PERF_TS_MEDIUM).
# Writes trace + top-N text under perf/traces/. One parse (~10 min), not full BDN warmup/iterations.
param(
    [string]$TsPath,
    [string]$TraceName = 'ParseFullTs_AllTables_9ts',
    [int]$TopN = 25
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if ($TsPath) { $env:TSPARSER_PERF_TS_MEDIUM = $TsPath }
if (-not $env:TSPARSER_PERF_TS_MEDIUM) { $env:TSPARSER_PERF_TS_MEDIUM = 'D:\Dvb\dvb_lib\9.ts' }

$env:DOTNET_TC_QuickJitForLoop = '0'

$traceDir = Join-Path $repoRoot 'perf\traces'
New-Item -ItemType Directory -Force -Path $traceDir | Out-Null
$traceOut = Join-Path $traceDir "$TraceName.nettrace"

Write-Host "Building TSParser.Benchmarks (Release x64)..."
dotnet build TSParser.Benchmarks\TSParser.Benchmarks.csproj -c Release -p:Platform=x64 -v q | Out-Null

$exe = Join-Path $repoRoot 'TSParser.Benchmarks\bin\x64\Release\net10.0\TSParser.Benchmarks.exe'
if (-not (Test-Path $exe)) { throw "Benchmark exe not found: $exe" }

if (Test-Path $traceOut) { Remove-Item $traceOut -Force }

Write-Host "Collecting CPU sample -> $traceOut"
Write-Host "TS: $env:TSPARSER_PERF_TS_MEDIUM"

dotnet trace collect --output $traceOut --profile cpu-sampling -- $exe --profile-once

$exclusive = Join-Path $traceDir "$TraceName`_top${TopN}_exclusive.txt"
$inclusive = Join-Path $traceDir "$TraceName`_top${TopN}_inclusive.txt"

dotnet trace report $traceOut topN -n $TopN | Out-File -Encoding utf8 $exclusive
dotnet trace report $traceOut topN -n $TopN --inclusive | Out-File -Encoding utf8 $inclusive

Write-Host "Exclusive top ${TopN}: $exclusive"
Write-Host "Inclusive top ${TopN}: $inclusive"
Get-Content $exclusive
