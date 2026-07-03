# Re-apply known-good ElonaKeys config values (in case the game rewrote stale ones on exit).
$f = "G:\SteamLibrary\steamapps\common\Elin\BepInEx\config\elin.elonakeys.cfg"
if (-not (Test-Path $f)) { exit 0 }
$lines = Get-Content $f | ForEach-Object {
    switch -Regex ($_) {
        "^OverrideGameKeys = false$" { "OverrideGameKeys = true" }
        "^DebugLog = false$"         { "DebugLog = true" }
        "^Zap = z$"                  { "Zap = shift+z" }
        "^Close = c$"                { "Close = shift+c" }
        "^ExamineInventory = x$"     { "ExamineInventory = shift+x" }
        default { $_ }
    }
}
Set-Content $f $lines
Write-Host "config verified"
