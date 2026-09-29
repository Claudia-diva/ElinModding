# Immersion Enhancement English

English labels for the Elin workshop mod 沉浸增强 (Immersion Enhancement,
`qf.elin.immersion_enhancement`, by 戏三子, MIT). That mod hardcodes its UI text
in Chinese, so its block of the Graphics config page reads Chinese whatever
language the game runs in.

This is a companion, not a fork. Keep your workshop subscription and keep
getting its updates: the labels are swapped as they reach the UI, keyed by the
stable English ids the mod already passes next to every label
(`AddToggle(button, "CellRgbLighting", "逐格 RGB 灯光增强", ...)`), so nothing
about the lighting itself is touched.

## What it relabels

25 toggles, 16 sliders, the two section headers (沉浸增强 / 沉浸参数) and the
bottom "restore preset" button. The id list is cross-checked against the
shipped DLL, not just the shipped source.

## Config (General)

- `Enabled` (default true): turn the relabeling off entirely.
- `KeepChineseInChineseGame` (default true): leave the author's text alone
  while the game itself runs in Chinese. Set false to force English there too.

## If the other mod updates

Each of the four hooks is applied independently and failure is non-fatal: a
renamed method logs a warning and that part of the UI stays Chinese. The mod
itself keeps working either way. If the mod is not installed at all, this
plugin loads, finds nothing, and does nothing.

## Build

```powershell
.\build.ps1                     # builds and installs into the Elin Package folder
.\build.ps1 -NoInstall          # build only
.\build.ps1 -ElinDir "D:\Elin"  # different game install
```

## Credit

The underlying mod is the work of 戏三子 and is MIT licensed. This project
contains none of its code, only English strings and the Harmony hooks that
apply them.
