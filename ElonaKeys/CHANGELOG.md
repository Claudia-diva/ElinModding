# Changelog

## 1.13.0 - 2026-07-05

- Elona window cycling: Tab walks the related-window family from your current position. Item menus cycle X(examine) → d → e → r → q → Z → t → o → B → T; the equipment window and the character sheet cycle into each other (feats are a tab inside the sheet, z/x); v (spells) and a (actions) cycle into each other.
- x by itself reopens the last-used item menu (defaults to examine inventory), classic Elona x. New LastMenu bind in the config.
- Quest boards simplified: up/down scroll the request list, left/right switch the board's tabs directly, and r presses the reroll button. No more group juggling there.
- The z wheel no longer fires the instant a key goes down: the direction confirms when you RELEASE. Holding up and then adding right (while up is still held) picks the top-right diamond; the pending diamond glows gold and grows slightly so you can see what will fire.

## 1.12.0 - 2026-07-05

- Tool holding fixed for real: t now holds through the game's hot-item system, so shift+direction with an axe chops trees (and harvests, tills, chops wood...) exactly like the pickaxe mines. Picking the held tool again puts it away.
- Targeting rework: throw and * now move a FREE tile cursor anywhere within your vision (arrows and numpad, iso-aware); Space jumps between characters (hostiles first), Enter confirms, and the hover info card follows the cursor. Throws no longer stick to a remembered ally: hostile acts only auto-target hostiles.
- Quick craft (float crafting window) fully keyboard-driven: the recipe list walks properly (position now derives from the list's own selection instead of resetting each frame), arrows navigate while WASD still walks, and ingredient/material dropdowns are reachable: Enter opens the option list, up/down pick, Enter/Esc close.
- Window tabs on z and x, Elona style (z back, x forward; x still examines when an item grid is focused). Works on the character sheet too. Tab is no longer consumed: it stays native for chat/input.
- Spell and action menus finally show each ability's sprite and its full native tooltip (same one as the ability window), plus current cost with the original in parentheses when they differ, casts left (spell stock) for spells, and potential % for actions.
- Item menus (read, quaff, zap, drop...) show Elona-style totals in the caption: piece count and total weight.
- shift+x is a real examine-inventory list now (everything you carry, equipped marked, Enter/x opens the stat card, totals in the caption) instead of toggling the native inventory.
- The toolbelt no longer appears in the wear screen.

## 1.11.4 - 2026-07-04

- The shift-page bar is now a clone of the real hotbar widget instead of a hand-drawn strip: same slot size, skin, icon rendering and shortcut numbers, and it follows the hotbar's icon-size setting. Slots are real hotbar slots (drag items onto them to assign to the other page). Position is saved as an offset from the main hotbar (drag the bar's empty space to move it); config keys renamed to ShiftBarOffsetX/Y, default is right above the hotbar.

## 1.11.3 - 2026-07-04

- Chests and containers now open the keyboard take/put menu no matter how you open them (walking into them, space, mouse click...). Locked chests still lockpick natively and surprise packages still spring their mimic. KeyboardContainers config to turn off.
- Item processors (mill, seed maker, grindstone, cooking tools...) open a keyboard ingredient picker on top of the native window; picking the ingredient(s) starts the craft exactly like a mouse transfer would. Esc leaves the native window for mouse use. KeyboardCrafters config.
- s on your own tile harvests/collects instantly when something is growing there (game's own harvest logic, tool-aware); otherwise it shows the tile menu with a "Search the ground" row (medal spots), so s always does something.
- c opens the character sheet, classic Elona style.
- Spell (v) and action (a) menus list your starred favorites first with no extra key press; the filter/favorites/native-window rows moved to the bottom. Left/right switch pages as before.
- Targeting mode (*) now feeds the selected character into the game's real mouse-hover pipeline every frame, so the mouseover info card and hover mods (e.g. Betterhovertext's level/stats/hobbies) display for the keyboard selection just like a mouse hover.

## 1.11.2 - 2026-07-04

- Crafting: recipe list finally keyboard-driven; up/down walk the FULL virtualized recipe list (auto-scrolls, selects each recipe like a click so the product panel updates live), left/right still switch to material slots and buttons, Tab still switches categories.
- Quickslot assignment now evicts an occupying ITEM back to the inventory before placing a spell (no more ghost item firing on shift+digit); activation prefers the ability when duplicates share a slot.
- Ranged ground-targeted spells (ice arrow and friends) no longer fall into the adjacent-direction prompt: they use the remembered target / targeting mode aimed at characters' tiles, with the direction prompt only as a last resort.
- Targeting mode shows target details (level, HP, distance, hostility) in a two-line strip; the Target key is also on the apostrophe (') for keyboards without a numpad.

## 1.11.1 - 2026-07-04

- Targeting flow rework: no windows in combat. Chara-targeted spells and throws fire INSTANTLY at your remembered target; without one, a light in-world targeting mode opens (slight dim, tile highlight, name strip, arrows cycle by distance, Enter confirms, Esc cancels). Confirming remembers the target.
- New Target key (classic *, default keypad-multiply): set/change the remembered target without acting.
- Party buffs cast on the whole party immediately (no member picker).
- Toolbelt protected: shown as (fixed) in the equipment window, cannot be removed there, excluded from throw.

## 1.11.0 - 2026-07-04

- Item colors fixed for real: menus now use the game's canonical UI item material (CoreRef.matUIObj) instead of borrowing from a live button whose state varied per save (the darkening).
- Casting honors each spell's real targeting: self spells cast instantly; party buffs offer "Cast on the whole party" (native multi-cast) or a single member; chara-targeted spells open a target menu (range/LOS enforced, hostiles/party marked); ground spells use the iso-aware direction prompt.
- Spell macros: Macro1-3 config sequences + MacroKey1-3 binds; casts each ability in order (self/party by type, targeted ones hit your current enemy).
- Digit hotkeys now use the QUICKSLOT bar: assignment (digit in menus/ability window) writes real toolbelt entries; plain digit = shown page, shift+digit = other page. shift+1..9 fires the other page without flipping. A draggable mini-bar shows the other page's icons (ShiftBarVisible).
- Native window navigation v4: up/down within a group, left/right between groups (lists / item grids / side buttons), Tab cycles window tabs (quest board, crafting categories), lists auto-scroll to the selection, drama talk choices navigable (native 1-9 selection also still works), quest reroll reachable via the side-button group.
- Space menu labels each action with its target ("Talk - Poppy").
- Equipment window: slot names stay static; only the item name scrolls.

## 1.10.0 - 2026-07-03

- Iso-aware aiming: direction prompts target the tile that LOOKS that way on screen (Elin's grid is rotated 45°; pressing down now hits visual south instead of grid-south/screen-southwest). IsoAim config, on by default; movement/bump keep native grid behavior.
- a = special actions (stamina, ABILITY group), v = spells (mana, SPELL group). Both lists: ★ favorites view, type filter (attack/buff/hex/heal/other via the game's own abilityType data), favorite management (Enter toggles the star), cost column with mp/sp, digit hotkeying, native window still one row away.
- Ammo stays on shift+a.

## 1.9.0 - 2026-07-03

- Wheel diamonds fixed (1.8.0 never assigned the label text) and restyled: transparent fill with a simple border.
- Containers: picking an item now offers Take / Drop at your feet / Move to another container (each with a count spinner), and "[ Container settings... ]" edits the game's own flags: cooler mode (only rottable), refuse rotten, always sort, distribution priority.
- Space = Elona context action: menu of everything interactable on and around you; a single action fires instantly (fountains, gacha machines, boards next to you).
- Bump-to-talk: walking into a friendly NPC talks to them (party members still swap). BumpToTalk config.
- Bump-to-use: walking into a solid interactable (quest board, gacha, fountain) uses it. BumpToUse config.

## 1.8.1 - 2026-07-03

- Containers (chests, boxes, bags) opened via o now use a keyboard take/put menu: item rows with count spinner to take, "[ Put items in... ]" opens your inventory with a spinner to store; shift walks back through the chain.
- Native windows that still open (crafting stations, mouse-opened containers) got grid navigation: arrows walk the item grid, Enter performs the transfer/use (what right-click does), x examines the item.

## 1.8.0 - 2026-07-03

- Wheel slots wear Elona-style diamonds (rotated plates with edge trim); the whole wheel is a draggable widget: drag the center diamond, position saved to config (WheelPosX/Y).
- Fixed native menus (e.g. the ability window) leaking through while a direction prompt was waiting: prompts are now fully modal.
- v opens the mod's own castable spell/ability list: everything the PC knows with cost column and category tag, Enter/letter casts through the game's targeting flow, digits hotkey the selected spell, native window reachable from the last row.

## 1.7.1 - 2026-07-03

- Wheel selection is instant 8-way: press a direction, that diamond fires; east/west diamonds switch rings. Misc ring added (rings hold max 6 verbs).

## 1.7.0 - 2026-07-03

- z wheel reworked into Elona's linked circles: Info / Action / Etc / Items rings, left/right (or clicking the side links) switches ring, up/down rotates, Enter fires, and the last-used ring is remembered like Elona. Each ring's verbs configurable (WheelInfo/WheelAction/WheelEtc/WheelItems); new verbs Help, Chara, Journal, Log, Fire.

## 1.6.3 - 2026-07-03

- Row layout matches Elona: sprite at far left, letter badge (revived, with its letter) next, name, weight as a true right-aligned column. Letters select through the game's own badge matching.

## 1.6.2 - 2026-07-03

- Attempted icon fix (superseded by 1.6.3).

## 1.6.0 - 2026-07-03

- z now opens a true radial action wheel (custom circle UI): arrows rotate, Enter/Space or click activates, z/Escape closes. WheelVerbs config picks what's on it (default 12 classic verbs); WheelStyle=list brings the window back.
- Bash rewired to Elona behavior: kick characters to shove them, kick closed doors open (locked doors thud), smash block-pass objects, kick the air otherwise.

## 1.5.3 - 2026-07-03

- Fixed a null tooltip-slot call that aborted every menu build after row one (broken Wear window, stale ghost rows). Row building is now exception-proof per row; weight renders inside the row text.

## 1.5.2 - 2026-07-03

- Native popups are keyboard-navigable: right-click context menus, yes/no dialogs and native list windows respond to arrows + Enter (arrows move a highlight, Enter clicks; untouched until you press an arrow, so mouse and default-Enter behavior stay intact).

## 1.5.1 - 2026-07-03

- Fixed Cast/Zap both bound to shift+z in upgraded configs (Cast is v); config repair script covers it.
- Wheel added to existing config files; all key descriptions and Mod Help refreshed to match current behavior.

## 1.5.0 - 2026-07-03

- Elona-style equipment window on w: body slot rows (Head/Body/Hand/...), "-" for empty, pick a slot to see candidates or remove; equip weight and DV/PV in the caption.
- Weight column on the right of every item menu.
- z inside a menu cycles sort: original / name / weight.
- Shift (released without being used as a modifier) goes back to the previous window, Elona style; window chains (wheel, give, categories, throw) keep a back-stack.
- Drop and Give use an inline count spinner like Elona's Input box: Enter on a stack, left/right ±1, up/down ±10, Enter confirms; defaults to all.
- Party items browse in our window: take items (with count) from members; character sheet entry kept.
- Preset quantity submenu removed (replaced by the spinner).

## 1.4.1 - 2026-07-03

- Item sprites finally render in menu rows (RenderRow.SetImage assumes the inventory-grid shader material and render pivots; sprites are now normalized: centered pivot, white color, scaled to the slot).
- Stat tooltip stays open while keyboard-browsing (TooltipManager.disableHide), instead of flashing for one frame.
- Quantity menu for stacks on Drop and Give: 1 / 5 / 10 / half / all rows, letters work; single items skip the question.
- z = action wheel: every mod verb in one menu, classic Elona style.

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
