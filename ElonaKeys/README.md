# Elona Keys

Classic Elona keybinds for [Elin](https://store.steampowered.com/app/2135150/Elin/). Play with the keyboard instead of dragging the mouse around.

## Default keys

| Key | Action |
|-----|--------|
| e | Eat (pick food from inventory) |
| q | Quaff a potion |
| r | Read a book or scroll |
| z | Zap a rod (then pick a direction, wait key = self) |
| Shift+Z | Cast (opens the ability/spell list) |
| g | Get everything on your tile |
| d | Drop an item |
| w | Wear / wield equipment |
| t | Hold an item in your hands (tools) |
| u | Use an item |
| o | Open an adjacent door (starts lockpicking if locked) |
| c | Close an adjacent door |
| b | Bash (pick a direction) |
| f | Fire ranged weapon (native action) |
| Shift+T | Throw an item (at your target, or pick a direction) |
| p | Pray |
| Shift+O | Offer at an adjacent altar |
| i | Interact menu for a direction (talk, give, trade...) |
| x | Examine inventory |
| Shift+. (>) | Descend stairs / enter zone on the world map |
| Shift+, (<) | Ascend stairs |

Movement stays on the numpad / arrow keys as in vanilla Elin. Item selection uses the game's native pick-an-item UI (the same one used for offerings); valid items are highlighted, click one to act. Escape or right-click cancels a direction prompt.

The game's own keybinds always win: if Elin's options map a key to something, that mapping is used and the mod bind is ignored. Rebind either side to resolve conflicts.

## Rebinding

Edit `BepInEx/config/elin.elonakeys.cfg` (created after first launch). Format: `e`, `shift+t`, `period`, `none` to unbind. Key names follow Unity's KeyCode enum.

## Install

Requires BepInEx and the Package Chainloader (`PackageLoader.dll`), which most Elin DLL-mod setups already have.

1. Run `build.ps1` (builds and copies to `Elin/Package/Mod_ElonaKeys`), or copy `ElonaKeys.dll` + `package.xml` there yourself.
2. Enable "Elona Keys" in the in-game mod list and restart.

## Build

```
./build.ps1                # build + install
./build.ps1 -NoInstall     # build only
./build.ps1 -ElinDir "D:\Games\Elin"
```

Needs the .NET SDK. References are resolved from the game folder, no NuGet packages.
