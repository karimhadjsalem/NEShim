<#
.SYNOPSIS
    Publishes NEShim and the seal-achievements tool as self-contained win-x64 binaries.

.PARAMETER Version
    Version number to stamp into the assemblies (e.g. 1.0.4 or v1.0.4).

.EXAMPLE
    .\publish.ps1 1.0.4
    .\publish.ps1 -Version 1.0.4
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

$Version = $Version.TrimStart('v')

$root    = $PSScriptRoot
$outBase = Join-Path $root "publish\v$Version"
$outGame = Join-Path $outBase "NEShim"
$outTool = Join-Path $outBase "SealAchievements"

$gameCsproj = Join-Path $root "NEShim\NEShim\NEShim.csproj"
$toolCsproj = Join-Path $root "NEShim\NEShim.SealAchievements\NEShim.SealAchievements.csproj"

Write-Host ""
Write-Host "NEShim v$Version — local publish"
Write-Host "  Game:   $outGame"
Write-Host "  Sealer: $outTool"
Write-Host ""

# ── Game ─────────────────────────────────────────────────────────────────────
Write-Host "Publishing game..."
dotnet publish $gameCsproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -p:Version=$Version `
    -o $outGame

if ($LASTEXITCODE -ne 0) {
    Write-Error "Game publish failed (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# ── Sealer tool ───────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Publishing seal-achievements..."
dotnet publish $toolCsproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -o $outTool

if ($LASTEXITCODE -ne 0) {
    Write-Error "Sealer publish failed (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Done. v$Version written to: $outBase"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Copy steam_api64.dll from the Steamworks.NET release zip into $outGame"
Write-Host "  2. Confirm steam_appid.txt in $outGame contains your production App ID"
Write-Host "  3. Seal achievements: .\publish\v$Version\SealAchievements\seal-achievements.exe --key-file private_key.txt achievements.json"
