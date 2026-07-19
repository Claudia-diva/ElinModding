# Build ElonaKeys and install it into the Elin Package folder.
#
# package.xml in the GAME folder is USER-OWNED: the install never overwrites
# it, it only patches the <version> line in place (workshop metadata like the
# title, id, tags and description are edited there and must survive builds).
# It is only created fresh from the repo copy when missing entirely.
param(
    [string]$ElinDir = "G:\SteamLibrary\steamapps\common\Elin",
    [switch]$NoInstall
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnet = Join-Path $HOME ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

& $dotnet build "$root\ElonaKeys.csproj" -c Release -p:ElinDir=$ElinDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $NoInstall) {
    $dest = Join-Path $ElinDir "Package\Mod_ElonaKeys"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item "$root\bin\Release\ElonaKeys.dll" $dest -Force
    Copy-Item "$root\package\*" $dest -Recurse -Force -Exclude "package.xml"
    $destMeta = Join-Path $dest "package.xml"
    if (Test-Path $destMeta) {
        $repoMeta = Get-Content "$root\package\package.xml" -Raw
        if ($repoMeta -match '<version>([0-9.]+)</version>') {
            $v = $Matches[1]
            $meta = Get-Content $destMeta -Raw
            $patched = $meta -replace '<version>[0-9.]+</version>', "<version>$v</version>"
            if ($patched -ne $meta) { Set-Content $destMeta -Value $patched -NoNewline }
        }
    }
    else {
        Copy-Item "$root\package\package.xml" $destMeta
    }
    Write-Host "Installed to $dest"
}
