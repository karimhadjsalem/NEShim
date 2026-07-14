<#
.SYNOPSIS
    Packages one game's content folder for the multi-game publish path — an alternate, additive
    path alongside the default single-game local-publish.ps1 flow. See CLAUDE.md's "Multi-Game
    Mode" section and the docs site's multi-game guide.

.DESCRIPTION
    Copies -SourceDir verbatim into games\<GameId>\ under -OutDir (or directly into an existing
    engine publish tree via -EngineOutDir). -SourceDir must contain a config.json; it is expected
    to be self-contained (ROM, config.json, achievements.json, artwork) — this script does not
    inspect or validate individual files beyond that one check.

    This script never touches the engine binary publish (local-publish.ps1) or NEShim.csproj —
    games\ content is never part of the MSBuild <Content> pipeline, exactly like config.json,
    the ROM, and achievements.json are not for a single-game publish today. Each game folder is
    built and versioned independently.

    Steam DLC is entirely OPTIONAL per game, not required by multi-game mode itself. Two
    distribution shapes are both first-class:
      - Per-game Steam DLC depots: run this script once per game (each with its own -OutDir/
        -EngineOutDir target), set each game's own steamDlcAppId, sell/bundle them as separate
        Steam DLC. Ownership is checked live via SteamApps.BIsDlcInstalled.
      - Single bundled deploy: run this script for every game against the SAME -EngineOutDir (or
        the same -OutDir), leave steamDlcAppId at its default 0 for all of them, and ship the one
        resulting folder as a single Steam depot (or standalone zip) — every game shows in the
        carousel unconditionally, no DLC entitlements involved at all.

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
    Write-Host "steamDlcAppId is 0 or unset — this game will show unconditionally (no Steam DLC ownership check)." -ForegroundColor Yellow
    Write-Host "  This is expected if you're bundling every game in a single deploy. If this game should" -ForegroundColor Yellow
    Write-Host "  instead be sold as separate Steam DLC, set steamDlcAppId before publishing." -ForegroundColor Yellow
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
Write-Host "  3. If (and only if) this game is sold as separate Steam DLC, set steamDlcAppId in"
Write-Host "     $dest\config.json to the matching Steam Partner DLC App ID, AND add the same"
Write-Host "     gameId/appId pair to games\multigame.json's gameDlcAppIds map. For real tamper-"
Write-Host "     proofing (recommended for a commercial release), also seal that map: compile a"
Write-Host "     public key into DlcMapSigner.EmbeddedPublicKeyBase64 (source-only, never a config"
Write-Host "     field) and rebuild, then run:"
Write-Host "       seal-achievements --seal-dlc-map --key-file private_key.txt games\multigame.json"
Write-Host "     See 'DLC ownership anti-tamper' in the docs. Leave steamDlcAppId at 0, with no"
Write-Host "     gameDlcAppIds entry, to bundle this game directly in the base install instead."
Write-Host "  4. Remember: this game's achievement steamIds must be namespaced/prefixed uniquely — all"
Write-Host "     games in a multi-game build share one Steam base App ID and therefore one achievement schema."
