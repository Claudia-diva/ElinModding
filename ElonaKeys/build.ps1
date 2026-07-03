# Build ElonaKeys and install it into the Elin Package folder.
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
    Copy-Item "$root\package\*" $dest -Recurse -Force
    Write-Host "Installed to $dest"
}
