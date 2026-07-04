# Re-apply known-good ElonaKeys config values (in case the game rewrote stale ones on exit).
$f = "G:\SteamLibrary\steamapps\common\Elin\BepInEx\config\elin.elonakeys.cfg"
if (-not (Test-Path $f)) { exit 0 }
$lines = Get-Content $f | ForEach-Object {
    switch -Regex ($_) {
        "^OverrideGameKeys = false$" { "OverrideGameKeys = true" }
        "^Zap = z$"                  { "Zap = shift+z" }
        "^Cast = shift\+z$"          { "Cast = v" }
        "^Close = c$"                { "Close = shift+c" }
        "^ExamineInventory = x$"     { "ExamineInventory = shift+x" }
        default { $_ }
    }
}
Set-Content $f $lines
Write-Host "config verified"
