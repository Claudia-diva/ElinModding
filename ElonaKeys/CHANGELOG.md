# Changelog

## 1.4.0 - 2026-07-03

- Throw offers a target list (visible characters sorted by distance, hostiles marked) before the direction fallback.
- Give reworked into a pure keyboard flow using the game's gift logic (refusals, affinity, cursed handling); one per pick from stacks.
- Fixed ping-pong name scrollers stacking up on pooled menu rows (jitter over time).
- Docs synced everywhere: README, in-game Mod Help ($About/$Keys/$Changelog), this file.

## 1.3.2 - 2026-07-03

- Key-chip badges deactivated entirely; selection letters live inside row labels.
- Item stat tooltip follows the keyboard cursor (ShowTooltipForced on selection).
- s and i open keyboard menus built from the tile's ActPlan instead of the mouse context popup.
- Direction prompts: tile highlights, held-movement-key release guard.
- Menus ignore the keystroke that opened them (no more instant self-selection like 'd' dropping row d).
- Repaired stale user config (OverrideGameKeys, Close/Zap/ExamineInventory binds).

## 1.3.1 - 2026-07-03

- Fixed abandoned direction prompts silently disabling all mod keys (now processed centrally with timeout and modal bail-out).
- Ability/spell hotbar placeholders excluded from all item menus (TraitAbility is a TraitTool subclass).
- Letters merged into row labels; cursor-based selection.

## 1.3.0 - 2026-07-03

- New menu system: fixed rows per page (MenuPageSize, default 15) so everything fits on screen, left/right arrows switch pages, up/down + Enter navigate and act, letters per page, icon-letter-name layout with the icon leftmost.
- x examine and 0-9 hotkey assignment now also work on the keyboard-selected row, not just mouse hover.
- Drop/wear menus refresh in place instead of closing and reopening.
- g pickup uses the game's own pickable-list for the tile (plants, installed pickables).
- New s key: menu of everything doable on your own tile (search spots, harvest, dig treasure, gather).
- New shift+a: load compatible ammo into your ranged weapon via menu.
- New shift+q: party member character sheets and inventories.
- Shift+direction with a held tool applies it to the adjacent tile like drag-right-click (ShiftDirTool, default on).
- SuppressNativeKeys config: swallow game keys entirely (e.g. 'z' examine-target if it annoys you).

## 1.2.1 - 2026-07-03

- Single-stage input routing with per-gate diagnostics (DebugLog on by default), narrower UI gates: keys no longer die when the mouse rests on widgets or after alt-tab.

## 1.1.0 - 2026-07-03

- Item selection reworked: scrollable Elona-style list menus (LayerList) with item icons and letter hotkeys, replacing the drag-grid "transfer box".
- Wear menu toggles equip/unequip, marks equipped items, stays open; favorited gear no longer blocked.
- Defaults corrected against the classic Elona key list: Zap=Z, Cast=v, Close=C; added Dig=D, Give=G, numpad-0 get; Tool=t is now the use-item menu.
- Quaff lists nearby wells/fountains via the game's own actDrink logic.
- Open now handles containers (with lockpicking) plus doors.
- Mod keys take priority over conflicting native keys (OverrideGameKeys, default on).
- Removed the redundant stairs keys.
- Multiple alternative keys per action supported in config (comma-separated).
- Added Mod Help panel pages (About / Keys / Changelog).

## 1.0.0 - 2026-07-03

- Initial release.
- Classic Elona keys: e/q/r/z/g/d/w/t/u/o/c/b/f/p/i/x, Shift+Z cast, Shift+T throw, Shift+O offer, Shift+./, stairs.
- Native item-selection UI (LayerDragGrid) for eat/quaff/read/zap/drop/wear/hold/use/throw.
- Direction prompts for zap/bash/throw/interact/open/close (when ambiguous).
- All binds configurable in BepInEx/config/elin.elonakeys.cfg; native game keybinds take priority on conflict.
