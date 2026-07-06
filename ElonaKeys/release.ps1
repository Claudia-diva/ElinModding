# ElonaKeys release pipeline (local "CI"): build, zip the mod package, tag,
# and publish a GitHub release with the zip attached. GitHub Actions cannot
# build this mod (references resolve from the installed game's DLLs, which
# must not be published), so releases are cut here and uploaded via the API.
#
#   ./release.ps1              # release the current version in Plugin.cs
#   ./release.ps1 -NoBuild     # reuse the existing build output
#   ./release.ps1 -Backfill    # note-only releases for every past CHANGELOG
#                              # version whose commit can be found (no zip)
#
# Token: GH_TOKEN env var (Claudia-diva). NEVER run with another account.
param(
    [switch]$NoBuild,
    [switch]$Backfill,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repo = 'Claudia-diva/ElinModding'
$root = $PSScriptRoot
$api = "https://api.github.com/repos/$repo"
$headers = @{
    Authorization = "Bearer $env:GH_TOKEN"
    Accept        = 'application/vnd.github+json'
    'User-Agent'  = 'ElonaKeys-release'
}

if (-not $env:GH_TOKEN) { throw 'GH_TOKEN is not set.' }

function Get-ChangelogNotes([string]$version) {
    $lines = Get-Content (Join-Path $root 'CHANGELOG.md')
    $notes = @()
    $in = $false
    foreach ($line in $lines) {
        if ($line -match "^## $([regex]::Escape($version)) ") { $in = $true; continue }
        if ($in -and $line -match '^## ') { break }
        if ($in) { $notes += $line }
    }
    ($notes -join "`n").Trim()
}

function New-GhRelease([string]$tag, [string]$title, [string]$notes, [string]$asset) {
    if ($DryRun) { "DRYRUN release $tag ($title) asset=$asset"; return }
    $existing = try { Invoke-RestMethod -Headers $headers -Uri "$api/releases/tags/$tag" } catch { $null }
    if ($existing) { "release $tag already exists, skipping"; return }
    $body = @{ tag_name = $tag; name = $title; body = $notes } | ConvertTo-Json
    $rel = Invoke-RestMethod -Method Post -Headers $headers -Uri "$api/releases" -Body $body -ContentType 'application/json'
    "created release $($rel.html_url)"
    if ($asset -and (Test-Path $asset)) {
        $name = Split-Path $asset -Leaf
        $up = $rel.upload_url -replace '\{.*\}', "?name=$name"
        Invoke-RestMethod -Method Post -Headers $headers -Uri $up -InFile $asset -ContentType 'application/zip' | Out-Null
        "uploaded $name"
    }
}

if ($Backfill) {
    # one note-only release per past CHANGELOG version with a findable commit
    $versions = (Select-String -Path (Join-Path $root 'CHANGELOG.md') -Pattern '^## ([0-9.]+) ').Matches |
        ForEach-Object { $_.Groups[1].Value }
    Push-Location $root
    try {
        foreach ($v in $versions) {
            $tag = "ElonaKeys-v$v"
            $commit = git log --format='%H' --grep "ElonaKeys $v" -n 1
            if (-not $commit) { "no commit found for $v, skipping"; continue }
            if (-not (git tag --list $tag)) {
                git tag $tag $commit
                if (-not $DryRun) { git push origin $tag } else { "DRYRUN push tag $tag -> $commit" }
            }
            New-GhRelease $tag "ElonaKeys $v" (Get-ChangelogNotes $v) $null
        }
    }
    finally { Pop-Location }
    return
}

# ---- current version release ----
$pluginSrc = Get-Content (Join-Path $root 'src\Plugin.cs') -Raw
if ($pluginSrc -notmatch 'Version = "([0-9.]+)"') { throw 'Version not found in Plugin.cs' }
$version = $Matches[1]
$tag = "ElonaKeys-v$version"

if (-not $NoBuild) { & (Join-Path $root 'build.ps1') -NoInstall }

# stage the shippable package layout: Mod_ElonaKeys/(dll + package files)
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'Mod_ElonaKeys'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $root 'bin\Release\ElonaKeys.dll') $stage
Copy-Item (Join-Path $root 'package\*') $stage -Recurse
$zip = Join-Path $dist "ElonaKeys_v$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip
"staged $zip"

Push-Location $root
try {
    if (git status --porcelain) { throw 'Working tree not clean: commit first.' }
    if (-not (git tag --list $tag)) {
        git tag $tag
        if (-not $DryRun) { git push origin $tag } else { "DRYRUN push tag $tag" }
    }
    New-GhRelease $tag "ElonaKeys $version" (Get-ChangelogNotes $version) $zip
}
finally { Pop-Location }
