<#
.SYNOPSIS
    Publishes NEShim and the pub-utils tools as self-contained binaries
    for all supported platforms.

.DESCRIPTION
    Outputs to publish\v<Version>\:
      NEShim-win-x64\          — game, Windows
      NEShim-linux-x64\        — game, Linux
      PubUtils-win-x64\   — pub-utils CLI + UI, Windows
      PubUtils-linux-x64\ — pub-utils CLI, Linux (UI is Windows-only)

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
$toolCsproj   = Join-Path $root "NEShim\NEShim.PubUtils\NEShim.PubUtils.csproj"
$toolUiCsproj = Join-Path $root "NEShim\NEShim.PubUtilsUI\NEShim.PubUtilsUI.csproj"

# PubUtilsUI targets net9.0-windows (Windows Forms) — win-x64 only.
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

# NTFS has no Unix executable-bit concept at all, so a linux-x64 publish produced on a Windows
# host can never carry +x as a plain folder - dotnet only chmods the apphost when actually
# running on a Unix host (a real syscall that doesn't exist here). A folder copy (Explorer,
# robocopy, a Windows-native scp/SFTP client) has nothing to preserve and lands non-executable
# every time, which is what prompted this: reproduced repeatedly copying NEShim-linux-x64 to a
# real Linux box. The fix isn't fixable via any dotnet/MSBuild setting - the bit has to be
# carried as DATA independent of the source filesystem. tar's per-entry mode field does exactly
# that: any real `tar xzf` on Linux restores it from the archive regardless of how the .tar.gz
# itself got transferred. Two-pass (bundled Git-for-Windows GNU tar, verified to correctly write
# 755 into the archive even though the Windows-hosted source files have no real mode bit): first
# pass archives everything except the entry binaries at their default mode, second pass appends
# just the entry binaries with --mode=755 into the same archive, then gzip.
#
# NOTE: this file must stay plain-ASCII. Windows PowerShell 5.1 (the default on Windows,
# distinct from pwsh/PowerShell 7+) reads a BOM-less .ps1 using the system codepage, not UTF-8 -
# a non-ASCII character (e.g. an em dash) landing inside a string literal here previously
# produced real, reproduced parse failures ("missing string terminator" / "missing closing
# brace") at actual script-execution time, not just a display glitch.
function New-LinuxTarball {
    param(
        [string]   $OutDir,
        [string[]] $Executables
    )

    $tarExe     = "C:\Program Files\Git\usr\bin\tar.exe"
    $gzipExe    = "C:\Program Files\Git\usr\bin\gzip.exe"
    $cygpathExe = "C:\Program Files\Git\usr\bin\cygpath.exe"
    if (-not (Test-Path $tarExe) -or -not (Test-Path $gzipExe) -or -not (Test-Path $cygpathExe)) {
        Write-Warning "  Git for Windows' tar/gzip/cygpath not found - skipping .tar.gz packaging for $OutDir."
        Write-Warning "  A plain folder copy to Linux will need a manual 'chmod +x' on: $($Executables -join ', ')"
        return
    }

    $present = $Executables | Where-Object { Test-Path (Join-Path $OutDir $_) }
    if ($present.Count -eq 0) { return }

    $tarPath   = "$OutDir.tar"
    $tarGzPath = "$OutDir.tar.gz"
    Remove-Item $tarPath, $tarGzPath -ErrorAction SilentlyContinue

    # GNU tar's MSYS2 build (a) treats a leading "C:" as a remote-host spec unless told
    # --force-local (a legacy rsh-era heuristic), and (b) doesn't reliably auto-translate a raw
    # Windows path containing both a colon and spaces passed as -C — reproduced directly:
    # "Cannot connect to C: resolve failed" without --force-local, then a mangled/unresolvable
    # path even with it until the path is explicitly converted via cygpath first.
    $outDirPosix  = & $cygpathExe -u $OutDir
    $tarPathPosix = & $cygpathExe -u $tarPath

    $excludeArgs = $present | ForEach-Object { "--exclude=$_" }
    & $tarExe --force-local -cf $tarPathPosix -C $outDirPosix @excludeArgs .
    & $tarExe --force-local --mode='755' -rf $tarPathPosix -C $outDirPosix @present
    & $gzipExe -f $tarPath

    Write-Host "  Packaged: $tarGzPath (entry binaries marked executable in the archive)"
}

Write-Host ""
Write-Host "NEShim v$Version - local publish"
Write-Host "  Platforms: $($gameRids -join ', ')"
Write-Host "  Output:    $outBase"
Write-Host ""

# ── Game ─────────────────────────────────────────────────────────────────────
Write-Host "Game:"
foreach ($rid in $gameRids) {
    $gameOutDir = Join-Path $outBase "NEShim-$rid"
    Invoke-Publish -Label 'NEShim' -Csproj $gameCsproj -Rid $rid `
        -OutDir $gameOutDir `
        -Version $Version -ReadyToRun
    if ($rid -eq 'linux-x64') {
        New-LinuxTarball -OutDir $gameOutDir -Executables @('NEShim', 'createdump')
    }
    Write-Host ""
}

# ── pub-utils CLI ───────────────────────────────────────────────────────────
Write-Host "pub-utils CLI:"
foreach ($rid in $toolRids) {
    $toolOutDir = Join-Path $outBase "PubUtils-$rid"
    Invoke-Publish -Label 'pub-utils' -Csproj $toolCsproj -Rid $rid `
        -OutDir $toolOutDir `
        -Version $Version
    if ($rid -eq 'linux-x64') {
        New-LinuxTarball -OutDir $toolOutDir -Executables @('pub-utils', 'createdump')
    }
    Write-Host ""
}

# ── pub-utils UI (Windows only) ───────────────────────────────────────────────
Write-Host "pub-utils UI:"
foreach ($rid in $toolUiRids) {
    Invoke-Publish -Label 'pub-utils-ui' -Csproj $toolUiCsproj -Rid $rid `
        -OutDir (Join-Path $outBase "PubUtils-$rid") `
        -Version $Version
    Write-Host ""
}

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host "Done. v$Version written to: $outBase"
Write-Host ""
Write-Host "For Linux testing: copy NEShim-linux-x64.tar.gz (not the NEShim-linux-x64\ folder"
Write-Host "directly) and extract with 'tar xzf' on the target - a plain folder copy from Windows"
Write-Host "can never carry the executable bit (NTFS has no such concept), which is why NEShim has"
Write-Host "needed a manual 'chmod +x' after every copy so far. The .tar.gz already has it set."
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Copy steam_api64.dll from the Steamworks.NET release zip into NEShim-win-x64\"
Write-Host "  2. Copy libsteam_api.so from the same release zip into NEShim-linux-x64\, RENAMED to libsteam_api64.so"
Write-Host "     (the wrapper's DllImport resolves to libsteam_api64.so; SteamAPI_Init fails silently under the zip's default name)"
Write-Host "  3. Set steam_appid.txt to your production App ID in each platform's game directory"
Write-Host "  4. Seal: .\publish\v$Version\PubUtils-win-x64\pub-utils.exe --key-file private_key.txt achievements.json"
Write-Host "  Note: NEShim-linux-x64.tar.gz was packaged before steps 2-3 touch that folder - for a"
Write-Host "  release build, re-tar it afterward (same two-pass 'tar --mode=755' approach this"
Write-Host "  script uses) so the Steam files and stamped App ID are included too."
