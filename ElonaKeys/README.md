# Elona Keys

Classic Elona keybinds for [Elin](https://store.steampowered.com/app/2135150/Elin/). Play with the keyboard instead of dragging the mouse around. Item keys open scrollable Elona-style menus with letter hotkeys.

## Default keys

Faithful to the [classic Elona key list](https://elona.fandom.com/wiki/Controls), minus what Elin already covers natively.

| Key | Action |
|-----|--------|
| g / numpad 0 | Get everything on your tile |
| d | Drop menu (stays open to drop several) |
| e | Eat menu |
| q | Quaff menu, includes nearby wells and fountains |
| r | Read menu |
| Z | Zap a rod, then pick a direction (wait key = self) |
| v | Cast (opens spell/ability list) |
| w | Wear/wield menu; click equipped items to remove them |
| t | Use an item |
| T | Throw (at your target, or pick a direction) |
| f | Fire ranged weapon (native action) |
| b | Bash a direction |
| D | Dig/mine a wall in a direction |
| o | Open a container or door (starts lockpicking if locked) |
| C | Close a door |
| p | Pray |
| O | Offer at an adjacent altar |
| G | Give items to an adjacent character |
| i | Interact menu for a direction (talk, give, trade...) |
| Shift+X | Inventory |

Movement stays on the numpad / arrow keys as in vanilla Elin. Character sheet, journal, log and help already have native Elin keys. In menus, click an entry or press its letter. Escape or right-click cancels a direction prompt.

Not covered yet: Dip (B) and ammo switching (A) have no clean Elin equivalent.

## Conflicts and rebinding

By default the mod's keys win over Elin's own bindings when they clash (classic `q` quaff beats the native empty-hand key). Set `OverrideGameKeys = false` in the config to flip that, or rebind either side.

Edit `BepInEx/config/elin.elonakeys.cfg` (created after first launch). Format: `e`, `shift+t`, `period`, comma-separated alternatives (`g, keypad0`), `none` to unbind. Key names follow Unity's KeyCode enum.

An in-game summary lives in the Mod Help panel (ESC menu) if you have Mod Help installed.

## Install

Requires BepInEx and the Package Chainloader (`PackageLoader.dll`), which most Elin DLL-mod setups already have.

1. Run `build.ps1` (builds and copies to `Elin/Package/Mod_ElonaKeys`), or copy `ElonaKeys.dll` + the contents of `package/` there yourself.
2. Enable "Elona Keys" in the in-game mod list and restart.

## Build

```
./build.ps1                # build + install
./build.ps1 -NoInstall     # build only
./build.ps1 -ElinDir "D:\Games\Elin"
```

Needs the .NET SDK. References are resolved from the game folder, no NuGet packages.
