# Elona Keys

Classic Elona keybinds for [Elin](https://store.steampowered.com/app/2135150/Elin/). Play with the keyboard; the mouse becomes optional.

Feature list and changelog are also available in-game via the **Mod Help** panel (ESC menu), and in [CHANGELOG.md](CHANGELOG.md).

## The menu system

Every item key opens a paginated Elona-style window:

- `►` cursor moves with **up/down**, **Enter** acts, **a-o letters** act directly, mouse click also works
- **left/right** (or PgUp/PgDn) switch pages; everything always fits on screen
- the selected item's full stat card shows automatically; **x** opens the examine window
- select/hover an item or spell and press **0-9** to bind it to a hotkey (shift+digit = slots 11-20)
- long names ping-pong scroll (`NameDisplay` config: pingpong/truncate/wrap)
- menus over `CategoryThreshold` items show a category filter first

Directional actions highlight the neighboring tiles and wait for a movement key; the wait key targets yourself, Escape or a click cancels.

## Default keys

| Key | Action |
|-----|--------|
| g / numpad 0 | Get everything pickable on your tile |
| d | Drop menu (stays open; whole stacks) |
| e / q / r | Eat / Quaff / Read menus (quaff includes wells) |
| Z | Zap a rod, then a direction (wait = self) |
| v | Cast: spell list, favorites first, sprites/tooltips/cost/casts left |
| a | Special actions (stamina), favorites first, potential shown |
| w | Wear/wield; picking an equipped item removes it |
| t | Hold a tool |
| u | Use an item |
| B | Dip/blend liquids with items |
| T | Throw: pick item, then free cursor (auto-fires at remembered hostile) |
| Shift+A | Load compatible ammo into your ranged weapon |
| f | Fire (native action) |
| b | Bash a direction |
| D | Dig a wall (auto-holds your digging tool) |
| o | Open containers/crafting kits, yours or nearby |
| C | Open or close a door in a chosen direction |
| s | Your tile: harvest/collect instantly, else menu + search the ground |
| c | Character sheet |
| * / ' | Free targeting cursor (any visible tile, Space cycles characters); hover info follows |
| z | Action circles: hold a direction, release fires (diagonals compose) |
| Space | Everything interactable on and around you |
| i | Interact with a direction (talk, trade, everything) |
| p / O | Pray / Offer at an altar |
| G | Give to an adjacent character (one per pick) |
| Shift+Q | Party member sheets and inventories |
| x | Reopen the last-used item menu (Elona x) |
| Shift+X | Examine inventory: full list with totals |
| Tab | Cycle window families: items X,d,e,r,q,Z,t,o,B,T / equipment-chara / spells-actions |

While family windows are open, an Elona-style diamond tab bar shows the whole family (current = gold, click to jump). The bar and the shift-page hotbar are real Elin widgets: toggle, drag, lock and anchor them in the game's widget manager; positions save with widget themes.
| Shift+0-9 | Fire hotkey slots 1-10 (also F1-F8 natively) |
| Shift+move | Use held tool on the adjacent tile |

Movement stays on numpad/arrows/WASD as in vanilla. Journal (j), log and help remain on Elin's own keys.

Chests and containers open the keyboard take/put menu no matter how you reach them, and item processors (mill, seed maker...) open a keyboard ingredient picker (`KeyboardContainers` / `KeyboardCrafters` configs).

## Config (`BepInEx/config/elin.elonakeys.cfg`)

- Every key rebindable: `e`, `shift+t`, `period`, comma-separated alternatives, `none`.
- `OverrideGameKeys` (default **true**): mod keys beat native Elin bindings on clash. If keys ever behave "native", check this first.
- `SuppressNativeKeys`: swallow game keys entirely (e.g. `z`).
- `ActivateHotkeys`: off / shift / plain digit firing; `ShiftDirTool`; `MenuPageSize`; `DebugLog` (explains every ignored keypress in `BepInEx/LogOutput.log`).

## Deliberate differences from classic Elona

- No quantity prompts (Elin has no native number picker): drop = whole stack, give = one per pick.
- No Rest key (Elin rests via beds / native Meditate).
- No separate look key; * targeting shows the same info card as mouse hover instead.
- Dip uses Elin's blend system, ammo switching uses Elin's reload.

## Install / Build

Requires BepInEx + Package Chainloader (standard Elin DLL-mod setup).

```
./build.ps1                # build + install to Elin/Package/Mod_ElonaKeys
./build.ps1 -NoInstall
./build.ps1 -ElinDir "D:\Games\Elin"
```

Needs the .NET SDK; references resolve from the game folder, no NuGet. Enable "Elona Keys" in the in-game mod list after installing.
