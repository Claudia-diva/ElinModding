# Changelog

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
