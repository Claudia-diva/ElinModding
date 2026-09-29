# Build ImmersionEnglish and install it into the Elin Package folder.
param(
    [string]$ElinDir = "G:\SteamLibrary\steamapps\common\Elin",
    [switch]$NoInstall
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnet = Join-Path $HOME ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

& $dotnet build "$root\ImmersionEnglish.csproj" -c Release -p:ElinDir=$ElinDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $NoInstall) {
    $dest = Join-Path $ElinDir "Package\Mod_ImmersionEnglish"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item "$root\bin\Release\ImmersionEnglish.dll" $dest -Force
    Copy-Item "$root\package\*" $dest -Recurse -Force
    Write-Host "Installed to $dest"
}
