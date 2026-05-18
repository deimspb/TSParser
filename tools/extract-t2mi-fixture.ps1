#Requires -Version 5.1
<#
.SYNOPSIS
  Build a small T2-MI test fixture from a full MPEG-TS capture.

.DESCRIPTION
  Copies up to -MaxPackets TS packets matching -TargetPid (default 0x1000) into
  TSParser.Tests/TestResources/T2mi/t2mi_cut_pid1000.ts for CI and offline tests.

  Set TSPARSER_T2MI_SAMPLE to the full source file when regenerating locally.

.EXAMPLE
  .\extract-t2mi-fixture.ps1
  $env:TSPARSER_T2MI_SAMPLE = 'D:\Dvb\dvb_lib\t2mi_cut.ts'; .\extract-t2mi-fixture.ps1
#>
[CmdletBinding()]
param(
    [string] $SourcePath = $(if ($env:TSPARSER_T2MI_SAMPLE) { $env:TSPARSER_T2MI_SAMPLE } else { 'D:\Dvb\dvb_lib\t2mi_cut.ts' }),
    [int] $TargetPid = 0x1000,
    [int] $MaxPackets = 800,
    [int] $PacketSize = 188,
    [string] $OutputPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputPath) {
    $OutputPath = Join-Path $RepoRoot 'TSParser.Tests\TestResources\T2mi\t2mi_cut_pid1000.ts'
}

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source TS not found: $SourcePath. Set TSPARSER_T2MI_SAMPLE or pass -SourcePath."
}

$bytes = [System.IO.File]::ReadAllBytes($SourcePath)
if ($bytes.Length -lt $PacketSize) {
    throw "Source file too small: $SourcePath"
}

$out = New-Object System.Collections.Generic.List[byte]
$matched = 0
for ($i = 0; $i -le $bytes.Length - $PacketSize; $i += $PacketSize) {
    if ($bytes[$i] -ne 0x47) {
        continue
    }

    $packetPid = (($bytes[$i + 1] -band 0x1F) -shl 8) -bor $bytes[$i + 2]
    if ($packetPid -ne $TargetPid) {
        continue
    }

    for ($j = 0; $j -lt $PacketSize; $j++) {
        [void]$out.Add($bytes[$i + $j])
    }

    $matched++
    if ($matched -ge $MaxPackets) {
        break
    }
}

if ($matched -eq 0) {
    throw "No packets found for PID 0x{0:X} in {1}" -f $TargetPid, $SourcePath
}

$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

[System.IO.File]::WriteAllBytes($OutputPath, $out.ToArray())
Write-Host ("Wrote {0} packets (PID 0x{1:X}, {2:N0} bytes) -> {3}" -f $matched, $TargetPid, $out.Count, $OutputPath)
