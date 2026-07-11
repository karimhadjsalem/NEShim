<#
.SYNOPSIS
    Publishes NEShim and the seal-achievements tools as self-contained binaries
    for all supported platforms.

.DESCRIPTION
    Outputs to publish\v<Version>\:
      NEShim-win-x64\          — game, Windows
      NEShim-linux-x64\        — game, Linux
      SealAchievements-win-x64\ — sealer CLI + UI, Windows
      SealAchievements-linux-x64\ — sealer CLI, Linux (UI is Windows-only)

.PARAMETER Version
    Version number to stamp into the assemblies (e.g. 1.0.4 or v1.0.4).

.EXAMPLE
    .\local-publish.ps1 1.0.4
    .\local-publish.ps1 -Version 1.0.4
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

$Version = $Version.TrimStart('v')

$root    = $PSScriptRoot
$outBase = Join-Path $root "publish\v$Version"

$gameCsproj   = Join-Path $root "NEShim\NEShim\NEShim.csproj"
$toolCsproj   = Join-Path $root "NEShim\NEShim.SealAchievements\NEShim.SealAchievements.csproj"
$toolUiCsproj = Join-Path $root "NEShim\NEShim.SealAchievementsUI\NEShim.SealAchievementsUI.csproj"

# SealAchievementsUI targets net9.0-windows (Windows Forms) — win-x64 only.
$gameRids   = @('win-x64', 'linux-x64')
$toolRids   = @('win-x64', 'linux-x64')
$toolUiRids = @('win-x64')

function Invoke-Publish {
    param(
        [string] $Label,
        [string] $Csproj,
        [string] $Rid,
        [string] $OutDir,
        [string] $Version,
        [switch] $ReadyToRun
    )
    Write-Host "  [$Rid] $Label..."
    $publishArgs = @(
        'publish', $Csproj,
        '-c', 'Release', '-r', $Rid, '--self-contained', 'true',
        "-p:Version=$Version", '-p:DebugType=none', '-o', $OutDir
    )
    if ($ReadyToRun) { $publishArgs += '-p:PublishReadyToRun=true' }
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$Label ($Rid) publish failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "NEShim v$Version — local publish"
Write-Host "  Platforms: $($gameRids -join ', ')"
Write-Host "  Output:    $outBase"
Write-Host ""

# ── Game ─────────────────────────────────────────────────────────────────────
Write-Host "Game:"
foreach ($rid in $gameRids) {
    Invoke-Publish -Label 'NEShim' -Csproj $gameCsproj -Rid $rid `
        -OutDir (Join-Path $outBase "NEShim-$rid") `
        -Version $Version -ReadyToRun
    Write-Host ""
}

# ── Sealer CLI ────────────────────────────────────────────────────────────────
Write-Host "seal-achievements CLI:"
foreach ($rid in $toolRids) {
    Invoke-Publish -Label 'seal-achievements' -Csproj $toolCsproj -Rid $rid `
        -OutDir (Join-Path $outBase "SealAchievements-$rid") `
        -Version $Version
    Write-Host ""
}

# ── Sealer UI (Windows only) ──────────────────────────────────────────────────
Write-Host "seal-achievements UI:"
foreach ($rid in $toolUiRids) {
    Invoke-Publish -Label 'seal-achievements-ui' -Csproj $toolUiCsproj -Rid $rid `
        -OutDir (Join-Path $outBase "SealAchievements-$rid") `
        -Version $Version
    Write-Host ""
}

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host "Done. v$Version written to: $outBase"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Copy steam_api64.dll from the Steamworks.NET release zip into NEShim-win-x64\"
Write-Host "  2. Set steam_appid.txt to your production App ID in each platform's game directory"
Write-Host "  3. Seal: .\publish\v$Version\SealAchievements-win-x64\seal-achievements.exe --key-file private_key.txt achievements.json"
