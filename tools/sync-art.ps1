<#
.SYNOPSIS
  Copies the brand files the launcher needs from the Nostalgia art repo into src/Nostalgia.Launcher/Assets/Brand/
  and writes Assets/Brand/ART_VERSION.txt with the art repo commit.

.DESCRIPTION
  The art repo (private, D:\daoc\art) is the source of truth. CI never sees it, so the launcher builds only from the
  synced copies committed here. Re-run after every art change, then commit Assets/Brand/.
  Never edit the copies by hand – change art/src, rebuild there (python src/build_all.py), commit, re-sync.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/sync-art.ps1
  powershell -ExecutionPolicy Bypass -File tools/sync-art.ps1 -ArtRepo E:\work\nostalgia-art
#>
param(
    [string]$ArtRepo = 'D:\daoc\art'
)
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $repoRoot 'src\Nostalgia.Launcher\Assets\Brand'

if (-not (Test-Path (Join-Path $ArtRepo 'README.md'))) { throw "Art repo not found: $ArtRepo" }

# source (relative to the art repo) -> target (relative to Assets/Brand)
$files = [ordered]@{
    'exports\background\nostalgia-bg-1280x720.png'   = 'nostalgia-bg-1280x720.png'
    'exports\background\nostalgia-bg-2560x1440.png'  = 'nostalgia-bg-2560x1440.png'
    'exports\logo\logo-horizontal-color.svg'         = 'logo-horizontal-color.svg'
    'exports\logo\logo-horizontal-color-800.png'     = 'logo-horizontal-color-800.png'
    'exports\logo\logo-horizontal-color-1600.png'    = 'logo-horizontal-color-1600.png'
    'exports\icon\nostalgia.ico'                     = 'nostalgia.ico'
    'src\fonts\Cinzel-SemiBold.ttf'                  = 'Fonts\Cinzel-SemiBold.ttf'
    'src\fonts\Cinzel-Bold.ttf'                      = 'Fonts\Cinzel-Bold.ttf'
    'src\fonts\Inter-Regular.ttf'                    = 'Fonts\Inter-Regular.ttf'
    'src\fonts\Inter-Medium.ttf'                     = 'Fonts\Inter-Medium.ttf'
    'src\fonts\Inter-SemiBold.ttf'                   = 'Fonts\Inter-SemiBold.ttf'
    'src\fonts\OFL-Cinzel.txt'                       = 'Fonts\OFL-Cinzel.txt'
    'src\fonts\OFL-Inter.txt'                        = 'Fonts\OFL-Inter.txt'
}

New-Item -ItemType Directory -Force (Join-Path $dest 'Fonts') | Out-Null
foreach ($src in $files.Keys) {
    $from = Join-Path $ArtRepo $src
    $to = Join-Path $dest $files[$src]
    if (-not (Test-Path $from)) { throw "Missing in art repo: $src" }
    Copy-Item -LiteralPath $from -Destination $to -Force
    Write-Host ("  {0,-48} -> Assets/Brand/{1}" -f $src, $files[$src].Replace('\', '/'))
}

# Commit of the art repo (+ marker if it has uncommitted changes)
$commit = (git -C $ArtRepo rev-parse --short=12 HEAD).Trim()
$dirty = git -C $ArtRepo status --porcelain
$when = (git -C $ArtRepo log -1 --format=%cI).Trim()
$version = if ($dirty) { "$commit-dirty" } else { $commit }

$lines = @(
    "art-commit: $version",
    "art-commit-date: $when",
    "synced: $((Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK'))",
    "source: nostalgia-art (direction A - Box-Art Dusk); re-sync with tools/sync-art.ps1, never edit these files by hand"
)
[System.IO.File]::WriteAllLines((Join-Path $dest 'ART_VERSION.txt'), $lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "ART_VERSION: $version"
if ($dirty) { Write-Warning 'The art repo has uncommitted changes – commit them there and sync again before a release.' }
