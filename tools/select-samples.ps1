#Requires -Version 5.1
<#
.SYNOPSIS
  Select up to four unique .tbl fixtures per table type (S, M1, M2, L) for unit tests.

.DESCRIPTION
  Scans a harvest staging tree, groups sections by table type, deduplicates by CRC32,
  picks size-based samples, copies to TSParser.Tests/TestResources/Tables/, and writes
  manifest.tables.json.

  Environment overrides:
    TSPARSER_TABLE_STAGING - harvest input (default: D:\Dvb\ts_harvest\tables)
    TSPARSER_TEST_FIXTURES - output Tables root (default: repo TSParser.Tests/TestResources)

.EXAMPLE
  .\select-samples.ps1
  .\select-samples.ps1 -StagingRoot D:\Dvb\ts_harvest\tables -WhatIf
#>
[CmdletBinding()]
param(
    [string] $StagingRoot = $(if ($env:TSPARSER_TABLE_STAGING) { $env:TSPARSER_TABLE_STAGING } else { 'D:\Dvb\ts_harvest\tables' }),
    [string] $FixturesRoot = $(if ($env:TSPARSER_TEST_FIXTURES) { $env:TSPARSER_TEST_FIXTURES } else { '' }),
    [int] $TargetSamples = 4,
    [switch] $WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $FixturesRoot) {
    $FixturesRoot = Join-Path $RepoRoot 'TSParser.Tests\TestResources'
}

$TablesOutRoot = Join-Path $FixturesRoot 'Tables'
$ManifestPath = Join-Path $FixturesRoot 'manifest.tables.json'

$KnownTypes = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('PAT', 'CAT', 'PMT', 'SDT', 'BAT', 'NIT', 'TDT', 'TOT', 'EIT', 'AIT', 'MIP', 'SCTE35', 'EWS', 'EEWS'),
    [StringComparer]::OrdinalIgnoreCase
)

$SampleLabels = @('S', 'M1', 'M2', 'L')

function Get-Crc32FromSectionBytes {
    param([byte[]] $Bytes)
    if ($Bytes.Length -lt 4) { return $null }
    $n = $Bytes.Length
    return [uint32](
        ([uint32]$Bytes[$n - 4] -shl 24) -bor
        ([uint32]$Bytes[$n - 3] -shl 16) -bor
        ([uint32]$Bytes[$n - 2] -shl 8) -bor
        [uint32]$Bytes[$n - 1]
    )
}

function Format-Hex32 {
    param([uint32] $Value)
    return ('0x{0:X}' -f $Value)
}

function Get-SectionLengthFromBytes {
    param([byte[]] $Bytes)
    if ($Bytes.Length -lt 3) { return $null }
    return [int]((([int]$Bytes[1] -band 0x0F) -shl 8) -bor [int]$Bytes[2])
}

function Get-TableTypeFromTableId {
    param([byte] $TableId)
    switch ($TableId) {
        0x00 { return 'PAT' }
        0x01 { return 'CAT' }
        0x02 { return 'PMT' }
        0x40 { return 'NIT' }
        0x41 { return 'NIT' }
        0x42 { return 'SDT' }
        0x46 { return 'SDT' }
        0x4A { return 'BAT' }
        0x70 { return 'TDT' }
        0x73 { return 'TOT' }
        0x74 { return 'AIT' }
        0x93 { return 'EWS' }
        0x94 { return 'EEWS' }
        0x95 { return 'EEWS' }
        0xFC { return 'SCTE35' }
        default {
            if ($TableId -ge 0x4E -and $TableId -le 0x6F) { return 'EIT' }
            return $null
        }
    }
}

function Get-TableTypeForFile {
    param(
        [System.IO.FileInfo] $File,
        [string] $StagingRoot
    )
    $relative = $File.FullName.Substring($StagingRoot.Length).TrimStart('\', '/')
    $parts = $relative -split '[\\/]'
    if ($parts.Count -ge 1 -and $KnownTypes.Contains($parts[0])) {
        return $parts[0].ToUpperInvariant()
    }

    if ($File.Name -match '^([A-Za-z0-9]+)_') {
        $prefix = $Matches[1].ToUpperInvariant()
        if ($KnownTypes.Contains($prefix)) { return $prefix }
    }

    $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    if ($bytes.Length -lt 1) { return $null }

  # MIP sections do not use a PSI table_id in byte 0; rely on folder/file name above.
    if ($parts.Count -ge 1 -and $parts[0].Equals('MIP', [StringComparison]::OrdinalIgnoreCase)) {
        return 'MIP'
    }
    if ($File.Name.StartsWith('MIP_', [StringComparison]::OrdinalIgnoreCase)) {
        return 'MIP'
    }

    return Get-TableTypeFromTableId -TableId $bytes[0]
}

function Get-SourceTsName {
    param(
        [System.IO.FileInfo] $File,
        [string] $StagingRoot
    )
    $relative = $File.FullName.Substring($StagingRoot.Length).TrimStart('\', '/')
    $parts = $relative -split '[\\/]'
    if ($parts.Count -ge 3) {
        return $parts[1]
    }
    return $null
}

function Get-SampleIndices {
    param([int] $Count, [int] $Target)
    if ($Count -le 0) { return @() }
    if ($Count -ge $Target) {
        return @(
            0,
            [int][Math]::Floor($Count / 3),
            [int][Math]::Floor((2 * $Count) / 3),
            ($Count - 1)
        ) | Select-Object -Unique
    }
    return 0..($Count - 1)
}

function Get-SampleLabels {
    param([int] $SelectedCount, [int] $Target)
    if ($SelectedCount -le 0) { return @() }
    if ($SelectedCount -ge $Target) {
        return $SampleLabels
    }
    return $SampleLabels[0..($SelectedCount - 1)]
}

if (-not (Test-Path -LiteralPath $StagingRoot)) {
    throw "Staging directory not found: $StagingRoot"
}

$stagingResolved = (Resolve-Path -LiteralPath $StagingRoot).Path
$tblFiles = @(Get-ChildItem -LiteralPath $stagingResolved -Filter '*.tbl' -File -Recurse)
if ($tblFiles.Count -eq 0) {
    Write-Warning "No .tbl files under $stagingResolved. Run harvest-tables.ps1 first."
    exit 0
}

Write-Host "Staging:  $stagingResolved"
Write-Host "Fixtures: $TablesOutRoot"
Write-Host "Found $($tblFiles.Count) .tbl file(s)"

$grouped = @{}
foreach ($file in $tblFiles) {
    $type = Get-TableTypeForFile -File $file -StagingRoot $stagingResolved
    if (-not $type) {
        Write-Warning "Skipping unrecognized table: $($file.FullName)"
        continue
    }
    if (-not $grouped.ContainsKey($type)) {
        $grouped[$type] = [System.Collections.Generic.List[object]]::new()
    }
    $grouped[$type].Add($file)
}

$manifest = [ordered]@{
    version     = 1
    generatedAt = (Get-Date).ToString('o')
    stagingRoot = $stagingResolved
    fixturesRoot = $FixturesRoot
    types       = [ordered]@{}
    tables      = [ordered]@{}
}

$totalCopied = 0

foreach ($type in ($grouped.Keys | Sort-Object)) {
    $files = $grouped[$type]
    $unique = [ordered]@{}

    foreach ($file in $files) {
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $crc = Get-Crc32FromSectionBytes -Bytes $bytes
        if ($null -eq $crc) { continue }

        $key = '{0:X8}' -f $crc
        if ($unique.Contains($key)) { continue }

        $unique[$key] = [pscustomobject]@{
            File            = $file
            Bytes           = $bytes
            Crc32           = $crc
            Size            = $bytes.Length
            SectionLength   = Get-SectionLengthFromBytes -Bytes $bytes
            TableId         = if ($bytes.Length -gt 0) { $bytes[0] } else { $null }
            SourceTs        = Get-SourceTsName -File $file -StagingRoot $stagingResolved
            StagingPath     = $file.FullName
        }
    }

    $sorted = @($unique.Values | Sort-Object Size, { $_.File.Name })
    $n = $sorted.Count
    $indices = @(Get-SampleIndices -Count $n -Target $TargetSamples)
    $labels = @(Get-SampleLabels -SelectedCount $indices.Count -Target $TargetSamples)

    $manifest.types[$type] = [ordered]@{
        available = $files.Count
        unique    = $n
        selected  = $indices.Count
        samples   = $indices.Count
    }

    if ($n -eq 0) {
        Write-Host "$type : no valid sections"
        continue
    }

    $typeDir = Join-Path $TablesOutRoot $type
    if (-not $WhatIf -and -not (Test-Path -LiteralPath $typeDir)) {
        New-Item -ItemType Directory -Path $typeDir -Force | Out-Null
    }

    Write-Host "$type : $n unique / $($files.Count) total -> selecting $($indices.Count)"

    if ($labels.Count -ne $indices.Count) {
        $labels = @(Get-SampleLabels -SelectedCount $indices.Count -Target $TargetSamples)
    }

    for ($i = 0; $i -lt $indices.Count; $i++) {
        $entry = $sorted[$indices[$i]]
        $label = $labels[$i]
        $destName = '{0}_{1}.tbl' -f $type, $label
        $relativePath = ('Tables/{0}/{1}' -f $type, $destName) -replace '\\', '/'
        $destPath = Join-Path $typeDir $destName

        if ($WhatIf) {
            Write-Host "  [WhatIf] $($entry.StagingPath) -> $destPath ($label, $($entry.Size) bytes)"
        }
        else {
            [System.IO.File]::WriteAllBytes($destPath, $entry.Bytes)
            $totalCopied++
        }

        $manifest.tables[$relativePath] = [ordered]@{
            relativePath   = $relativePath
            type           = $type
            sample         = $label
            size           = $entry.Size
            crc32          = (Format-Hex32 -Value $entry.Crc32)
            sectionLength  = $entry.SectionLength
            tableId        = if ($null -ne $entry.TableId) { ('0x{0:X2}' -f $entry.TableId) } else { $null }
            sourceTs       = $entry.SourceTs
            sourceStagingPath = $entry.StagingPath
        }
    }
}

if (-not $WhatIf) {
    if (-not (Test-Path -LiteralPath (Split-Path $ManifestPath -Parent))) {
        New-Item -ItemType Directory -Path (Split-Path $ManifestPath -Parent) -Force | Out-Null
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
}

Write-Host ''
Write-Host 'Selection complete.'
Write-Host "  Types:    $($grouped.Keys.Count)"
Write-Host "  Copied:   $totalCopied"
Write-Host "  Manifest: $ManifestPath"

exit 0
