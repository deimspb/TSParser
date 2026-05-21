# Publish TSParser.Desktop self-contained builds for common RIDs.
# Usage (from repo root):
#   .\tools\publish-desktop.ps1
#   .\tools\publish-desktop.ps1 -Rid win-x64
#   .\tools\publish-desktop.ps1 -Configuration Debug

param(
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')]
    [string[]] $Rid = @('win-x64', 'linux-x64', 'osx-arm64'),
    [string] $Configuration = 'Release',
    [string] $OutputRoot = 'artifacts/desktop'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'TSParser.Desktop\TSParser.Desktop.csproj'

Push-Location $repoRoot
try {
    foreach ($r in $Rid) {
        $outDir = Join-Path $repoRoot "$OutputRoot\$r"
        Write-Host "Publishing $r -> $outDir"
        dotnet publish $project `
            -c $Configuration `
            -r $r `
            --self-contained true `
            -o $outDir
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    Write-Host "Done. Outputs under $OutputRoot"
}
finally {
    Pop-Location
}
