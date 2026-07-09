# Elona Keys

Classic Elona keybinds for [Elin](https://store.steampowered.com/app/2135150/Elin/). Play with the keyboard; the mouse becomes optional.

Feature list and changelog are also available in-game via the **Mod Help** panel (ESC menu), and in [CHANGELOG.md](CHANGELOG.md).

## The menu system

Every item key opens a paginated Elona-style window:

- `►` cursor moves with **up/down** (hold to keep scrolling), **Enter** acts, **a-o letters** act directly, mouse click also works
- **left/right** (or PgUp/PgDn) switch pages; the caption shows page and totals ("· 24 items 13.5s")
- the selected item's full stat card shows automatically; **x** opens the examine window
- **Tab** cycles the window family from your current position: items `X,d,e,r,q,Z,t,o,B,T` / equipment↔character sheet / spells↔actions. A diamond tab bar (real Elin widget) shows the family while its windows are open; click a diamond to jump
- **z** cycles sorting (original / name / weight); **Shift** goes back through the window chain
- stacks open a « n (max) » count spinner: left/right ±1, up/down ±10 (hold to repeat), Enter confirms
- select an item or spell and press **0-9** to assign it to a quickslot (shift+digit = the other page)
- menus over `CategoryThreshold` items show a category filter first; long names ping-pong scroll

## Default keys

| Key | Action |
|-----|--------|
| numpad 0 | Instantly pick up EVERYTHING on your tile (owned goods prompt once) |
| g | Pick up: single item instant, several = selection menu; HOLD g = grab anything nearby (furniture, dropped containers) |
| d | Drop menu |
| e / q / r | Eat / Quaff / Read menus (quaff includes wells) |
| Z | Zap a rod |
| v | Cast: spells with sprite and Lv / Attr / Pow / Hit / Dmg / Cost / Left columns (order via `AbilityColumns`); hold v (or press x on a row) = manual aim at anyone, allies included |
| a | Special actions (same columns, sp cost + potential %) |
| Shift+W | Wide Skill: list everything castable on your whole party, Enter casts on all |
| Shift+E | Empty your hands (held item/tool, grabbed furniture, stuck hotbar selection) |
| w | Equipment window: body slots, pick one to equip/remove |
| t | Hold ANY usable item (brush, whip, syringe, tools...), then i / Space / s applies it; pick again to put away |
| u | Use an item on yourself |
| B | Dip/blend liquids with items |
| T | Throw: pick item, then the free cursor (auto-fires at your remembered hostile) |
| Shift+A | Load compatible ammo into your ranged weapon |
| f | Fire (native action) |
| b | Bash a direction |
| D | Dig a wall (auto-holds your digging tool) |
| o | Open containers (keyboard take/put/settings menu) |
| C | Open or close a door |
| s | Your tile: harvest/collect instantly, else menu + search the ground |
| c | Character sheet (z/x switch its tabs) |
| x | Reopen the last-used item menu (Elona x) |
| Shift+X | Examine inventory: full list with totals |
| * / ' | Free targeting cursor (any visible tile, Space cycles characters); hover info follows |
| z | Action circles: hold a direction, release fires (diagonals compose) |
| Space | Everything interactable on and around you |
| i | Interact with a direction (talk, trade, held-item acts, everything) |
| p / O | Pray / Offer at an altar |
| G | Give to an adjacent character |
| Shift+Q | Party member sheets and inventories |
| Shift+1-9 | Fire a quickbind (mod registry, resolved by kind at press time; corner icons on the hotbar show them; bind with digits inside menus) |
| Shift+move | Use held tool on the adjacent tile (chop, mine, till...) |
| Tab | Cycle window families; native chat otherwise |

Movement stays on numpad/arrows/WASD as in vanilla. Journal (j), log and help remain on Elin's own keys.

## Beyond the keys

- **Shops are ElonaKeys windows** (nothing vanilla opens): stock with prices, Enter buys (count spinner for stacks), Tab flips to the sell view with payout prices (this-visit purchases sell back at what you paid), your money (or base resource) in the footer. Covers every vendor: shopkeepers, wandering merchants, spell vendors, the duplication shops, home-resource boards.
- **Ally trading**: "Trade" on a party member or pet opens a two-pane menu (their items / yours, Tab switches, spinner moves stacks).
- **Every pick-an-item flow** (altar offerings, dip/blend, identify/uncurse/enchant/material scrolls, wizard services, bank deposit, tax box, quest chests, gacha, recycle, gene machine, mod kit, refuel) opens an ElonaKeys item list; services charge their normal fee.
- **Held items are generic**: whatever the game (or another mod) lets you right-click with while holding, the same acts appear through i / Space / s. No per-item special cases.
- **Chests/containers** open the keyboard take/put menu from ANY route (bump, space, mouse); locked chests lockpick natively. Container settings cover the full native set: sort mode/ascending/always, distribution priority, cooler mode, refuse rotten, advanced distribution, per-category allow/block, autodump mode, name filter.
- **Crafting is Elona-style**: stations and quick craft open a keyboard recipe list (craftable first, skill + max count); Enter a recipe to cycle each ingredient through every owned material (inventory + home storage) and craft with a count spinner. Native window on the last row; `KeyboardCrafting` config.
- **Item processors** (mill, seed maker...) open a keyboard ingredient picker (home storage included at your base); picking ingredients starts the craft.
- **Inventory browser** (shift+x): period/comma cycle You → party members → Home stock (z sorts, x examines, Tab hops the window family as usual); take items with the count spinner. numpad0 anywhere = act with the whole stack, no spinner.
- **Native windows** (dialogs, drama choices, quest boards, crafting, quick craft): arrows navigate in reading order (hold to scroll), left/right switch groups, z/x switch tabs, Enter acts, dropdown pickers open with Enter and pick with arrows. Quest boards: up/down scroll, left/right tabs, r rerolls.
- **Targeting** is a free tile cursor within your vision; confirmed characters are remembered and hostile acts never auto-target allies. The hover info card (and hover mods) follow the cursor.
- **Widgets**: the family tab bar is a real Elin widget (full native style menu: skin background, size, color, decorations; manage it in the widget manager, positions save with themes). The other quickslot page shows as corner icons on the real hotbar (`ShiftBarVisible`).
- **Macros**: Macro1-3 config sequences cast ability chains on one key.

## Config (`BepInEx/config/elin.elonakeys.cfg`)

- Every key rebindable: `e`, `shift+t`, `period`, comma-separated alternatives, `none`.
- `OverrideGameKeys` (default **true**): mod keys beat native Elin bindings on clash. If keys ever behave "native", check this first.
- `SuppressAllKey` (default **CapsLock**, unused by the game): PAUSES the whole mod. While ON, everything is pure native Elin (windows, keys, no redirects) until pressed again. The escape hatch if any mod window ever misbehaves.
- `SuppressNativeKeys`: swallow game keys entirely. `KeyboardContainers` / `KeyboardCrafters`: the container/processor redirects. `FamilyBarVisible` / `ShiftBarVisible` / `FamilyBarScale` / `FamilyBarHint`: widget/overlay options. `IsoAim`, `BumpToTalk`, `BumpToUse`, `ShiftDirTool`, `MenuPageSize`, `NameDisplay`, `MacroN`.
- `DebugLog` explains every ignored keypress in `BepInEx/LogOutput.log`.

## Deliberate differences from classic Elona

- Quantity via the arrow-key spinner instead of typed numbers (Elin has no native number picker).
- No Rest key (Elin rests via beds / native Meditate).
- No separate look key; * targeting shows the same info card as mouse hover instead.
- Dip uses Elin's blend system, ammo switching uses Elin's reload.

## Boundaries (mouse land, for now)

- Build/designation modes (construction, zone designations, deconstruct, move-installed, the house editor) are mouse-driven; mod keys sleep there.
- Pixel paint, casino games, the PCC editor and the jukebox are inherently mouse activities.

## Install / Build

Requires BepInEx + Package Chainloader (standard Elin DLL-mod setup).

```
./build.ps1                # build + install to Elin/Package/Mod_ElonaKeys
./build.ps1 -NoInstall
./build.ps1 -ElinDir "D:\Games\Elin"
```

Needs the .NET SDK; references resolve from the game folder, no NuGet. Enable "Elona Keys" in the in-game mod list after installing.
