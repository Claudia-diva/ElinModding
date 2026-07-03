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
| v | Cast (opens spell/ability list; hover a spell + 0-9 to hotkey it) |
| w | Wear/wield menu; click equipped items to remove them |
| t | Hold a tool in your hands |
| u | Use an item |
| B | Dip/blend: pick a liquid, then mix items into it |
| T | Throw (at your target, or pick a direction) |
| f | Fire ranged weapon (native action) |
| b | Bash a direction |
| D | Dig/mine a wall (auto-holds your digging tool) |
| o | Open containers and crafting kits, in your inventory or nearby |
| C | Open or close a door in a chosen direction |
| p | Pray |
| O | Offer at an adjacent altar |
| G | Give items to an adjacent character |
| i | Interact menu for a direction (talk, give, trade...) |
| Shift+X | Inventory |

Movement stays on the numpad / arrow keys as in vanilla Elin. Character sheet, journal, log and help already have native Elin keys. Escape or right-click cancels a direction prompt.

In menus: click an entry or press its letter; hover + `x` examines the item; hover + `0-9` assigns it to an Extra-hotbar hotkey slot (shift+digit for slots 11-20). Fire hotkeys with `shift+0-9` (default), `F1-F8` (native), or bare digits with `ActivateHotkeys=plain`. Long names ping-pong scroll by default (`NameDisplay` = pingpong/truncate/wrap). Menus over 25 items show a category filter first (`CategoryFilter`, `CategoryThreshold`).

Not covered yet: ammo switching (A) has no clean Elin equivalent.

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
