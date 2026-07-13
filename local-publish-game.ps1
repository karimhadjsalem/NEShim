<#
.SYNOPSIS
    Packages one game's content folder for the multi-game (DLC) publish path — an alternate,
    additive path alongside the default single-game local-publish.ps1 flow. See
    CLAUDE.md's "Multi-Game Mode" section and the docs site's multi-game guide.

.DESCRIPTION
    Copies -SourceDir verbatim into games\<GameId>\ under -OutDir (or directly into an existing
    engine publish tree via -EngineOutDir). -SourceDir must contain a config.json; it is expected
    to be self-contained (ROM, config.json, achievements.json, artwork) — this script does not
    inspect or validate individual files beyond that one check.

    This script never touches the engine binary publish (local-publish.ps1) or NEShim.csproj —
    games\ content is never part of the MSBuild <Content> pipeline, exactly like config.json,
    the ROM, and achievements.json are not for a single-game publish today. Each game folder is
    built and versioned independently, matching one Steam Partner DLC depot per games\<GameId>\.

.PARAMETER GameId
    Stable folder name for this game under games\ — becomes the carousel's internal game
    identifier (distinct from its display title, which comes from config.json's
    gameDisplayTitle/windowTitle).

.PARAMETER SourceDir
    Directory containing this game's ROM, config.json, achievements.json, and artwork.

.PARAMETER OutDir
    Destination for games\<GameId>\. Mutually exclusive with -EngineOutDir.

.PARAMETER EngineOutDir
    An existing engine publish directory (e.g. publish\v1.0\NEShim-win-x64) to drop
    games\<GameId>\ into directly. Mutually exclusive with -OutDir.

.EXAMPLE
    .\local-publish-game.ps1 -GameId kaaz -SourceDir .\game-content\kaaz -OutDir .\publish\v1.0\games\kaaz
    .\local-publish-game.ps1 -GameId kaaz -SourceDir .\game-content\kaaz -EngineOutDir .\publish\v1.0\NEShim-win-x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameId,

    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [string]$OutDir,

    [string]$EngineOutDir
)

if ([string]::IsNullOrWhiteSpace($OutDir) -eq [string]::IsNullOrWhiteSpace($EngineOutDir)) {
    Write-Error "Specify exactly one of -OutDir or -EngineOutDir."
    exit 1
}

if (-not (Test-Path $SourceDir)) {
    Write-Error "SourceDir not found: $SourceDir"
    exit 1
}

$configPath = Join-Path $SourceDir "config.json"
if (-not (Test-Path $configPath)) {
    Write-Error "SourceDir must contain a config.json: $configPath"
    exit 1
}

$config = Get-Content $configPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($config.gameDisplayTitle)) {
    Write-Warning "gameDisplayTitle is not set in config.json — the carousel will fall back to windowTitle."
}
if (-not $config.steamDlcAppId -or $config.steamDlcAppId -eq 0) {
    Write-Warning "steamDlcAppId is 0 or unset — this game will show unconditionally, without a real Steam DLC ownership check."
}

$destRoot = if ($OutDir) { $OutDir } else { Join-Path $EngineOutDir "games" }
$dest     = Join-Path $destRoot $GameId

Write-Host ""
Write-Host "NEShim multi-game content publish"
Write-Host "  GameId: $GameId"
Write-Host "  Source: $SourceDir"
Write-Host "  Dest:   $dest"
Write-Host ""

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item -Path (Join-Path $SourceDir "*") -Destination $dest -Recurse -Force

Write-Host "Done."
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Make sure the engine publish (local-publish.ps1) ships games\multigame.json — its"
Write-Host "     presence is what activates multi-game/carousel mode; it is not created by this script."
Write-Host "  2. Seal this game's achievements: seal-achievements --key-file private_key.txt `"$dest\achievements.json`""
Write-Host "  3. Set steamDlcAppId in $dest\config.json to the matching Steam Partner DLC App ID."
Write-Host "  4. Remember: this game's achievement steamIds must be namespaced/prefixed uniquely — all"
Write-Host "     games in a multi-game build share one Steam base App ID and therefore one achievement schema."
