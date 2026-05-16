#Requires -Version 5.1
<#
.SYNOPSIS
  Batch-extract SI tables from a TS corpus via StreamParser into a staging directory.

.DESCRIPTION
  Runs StreamParser (file mode, -o file) for each .ts file and each supported table type.
  Output layout: <StagingRoot>\<Type>\<tsBaseName>\{Type}_{n}_{timestamp}.tbl

  Environment overrides:
    TSPARSER_TS_ROOT       - input .ts directory (default: D:\Dvb\dvb_lib)
    TSPARSER_TABLE_STAGING - output root (default: D:\Dvb\ts_harvest\tables)
    STREAMPARSER_EXE       - path to StreamParser.exe

.EXAMPLE
  .\harvest-tables.ps1
  .\harvest-tables.ps1 -TsRoot D:\Dvb\dvb_lib -RunTimeMs 120000
  .\harvest-tables.ps1 -IncludeEws -EwsPids 793,794 -EewsPids 795
#>
[CmdletBinding()]
param(
    [string] $TsRoot = $(if ($env:TSPARSER_TS_ROOT) { $env:TSPARSER_TS_ROOT } else { 'D:\Dvb\dvb_lib' }),
    [string] $StagingRoot = $(if ($env:TSPARSER_TABLE_STAGING) { $env:TSPARSER_TABLE_STAGING } else { 'D:\Dvb\ts_harvest\tables' }),
    [string] $StreamParserExe = $env:STREAMPARSER_EXE,
    [int] $RunTimeMs = 60000,
    [switch] $IncludeEws,
    [string] $EwsPids = '',
    [string] $EewsPids = '',
    [switch] $WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$TableTypes = @(
    'PAT', 'CAT', 'PMT', 'SDT', 'BAT', 'NIT', 'TDT', 'TOT', 'EIT', 'AIT', 'MIP', 'SCTE35'
)

function Resolve-StreamParserExe {
    param([string] $Override)
    if ($Override -and (Test-Path -LiteralPath $Override)) {
        return (Resolve-Path -LiteralPath $Override).Path
    }
    $candidates = @(
        (Join-Path $RepoRoot 'StreamParser\bin\Release\net10.0\StreamParser.exe'),
        (Join-Path $RepoRoot 'StreamParser\bin\Debug\net10.0\StreamParser.exe'),
        (Join-Path $RepoRoot 'StreamParser\bin\Release\net10.0\win-x64\StreamParser.exe')
    )
    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) {
            return (Resolve-Path -LiteralPath $path).Path
        }
    }
    throw @"
StreamParser.exe not found. Build the StreamParser project (Release) or set STREAMPARSER_EXE.

  dotnet build -c Release ""$RepoRoot\StreamParser\StreamParser.csproj""
"@
}

function Invoke-StreamParserHarvest {
    param(
        [string] $Exe,
        [string] $TsFile,
        [string] $DecodeType,
        [string] $OutDir,
        [int] $RunMs,
        [string[]] $ExtraArgs
    )
    if (-not (Test-Path -LiteralPath $OutDir)) {
        New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
    }
    $args = @(
        'file',
        '-f', $TsFile,
        '-d', $DecodeType,
        '-o', 'file',
        '--dir', $OutDir,
        '--run_time', $RunMs
    ) + $ExtraArgs

    $display = "$Exe $($args -join ' ')"
    if ($WhatIf) {
        Write-Host "[WhatIf] $display"
        return @{ Ok = $true; Skipped = $true }
    }

    Write-Host ">> $DecodeType : $(Split-Path -Leaf $TsFile)"
    $null = & $Exe @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "StreamParser exited with code $LASTEXITCODE for $DecodeType / $(Split-Path -Leaf $TsFile)"
        return @{ Ok = $false; Skipped = $false }
    }
    return @{ Ok = $true; Skipped = $false }
}

if (-not (Test-Path -LiteralPath $TsRoot)) {
    throw "TS corpus directory not found: $TsRoot"
}

$sp = Resolve-StreamParserExe -Override $StreamParserExe
Write-Host "StreamParser: $sp"
Write-Host "TS root:        $TsRoot"
Write-Host "Staging root:   $StagingRoot"
Write-Host "Run time (ms):  $RunTimeMs"

$tsFiles = @(Get-ChildItem -LiteralPath $TsRoot -Filter '*.ts' -File | Sort-Object Name)
if ($tsFiles.Count -eq 0) {
    Write-Warning "No .ts files under $TsRoot"
    exit 0
}

$stats = @{
    TsFiles   = $tsFiles.Count
    Attempts  = 0
    Succeeded = 0
    Failed    = 0
}

foreach ($ts in $tsFiles) {
    $tsBase = [System.IO.Path]::GetFileNameWithoutExtension($ts.Name)
    foreach ($t in $TableTypes) {
        $outDir = Join-Path $StagingRoot (Join-Path $t $tsBase)
        $stats.Attempts++
        $result = Invoke-StreamParserHarvest -Exe $sp -TsFile $ts.FullName -DecodeType $t `
            -OutDir $outDir -RunMs $RunTimeMs -ExtraArgs @()
        if ($result.Ok) { $stats.Succeeded++ } else { $stats.Failed++ }
    }
}

if ($IncludeEws) {
    if (-not $EwsPids -and -not $EewsPids) {
        Write-Warning 'IncludeEws set but no --ews_pid / --eews_pid values. Run pidList harvest first or pass -EwsPids / -EewsPids.'
    }
    foreach ($ts in $tsFiles) {
        $tsBase = [System.IO.Path]::GetFileNameWithoutExtension($ts.Name)
        if ($EwsPids) {
            $outDir = Join-Path $StagingRoot (Join-Path 'EWS' $tsBase)
            $stats.Attempts++
            $result = Invoke-StreamParserHarvest -Exe $sp -TsFile $ts.FullName -DecodeType 'EWS' `
                -OutDir $outDir -RunMs $RunTimeMs -ExtraArgs @('--ews_pid', $EwsPids)
            if ($result.Ok) { $stats.Succeeded++ } else { $stats.Failed++ }
        }
        if ($EewsPids) {
            $outDir = Join-Path $StagingRoot (Join-Path 'EEWS' $tsBase)
            $stats.Attempts++
            $result = Invoke-StreamParserHarvest -Exe $sp -TsFile $ts.FullName -DecodeType 'EEWS' `
                -OutDir $outDir -RunMs $RunTimeMs -ExtraArgs @('--eews_pid', $EewsPids)
            if ($result.Ok) { $stats.Succeeded++ } else { $stats.Failed++ }
        }
    }
}

Write-Host ''
Write-Host 'Harvest complete.'
Write-Host "  TS files:   $($stats.TsFiles)"
Write-Host "  Attempts:   $($stats.Attempts)"
Write-Host "  Succeeded:  $($stats.Succeeded)"
Write-Host "  Failed:     $($stats.Failed)"
Write-Host "  Staging:    $StagingRoot"

if ($stats.Failed -gt 0) { exit 1 }
exit 0
