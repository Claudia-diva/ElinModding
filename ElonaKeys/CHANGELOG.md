# Changelog

## 1.23.1 - 2026-07-08
- Pow column in the spell/action lists (with its header): the effective power, the number that grows with your skill and spell levels and gear. Sits between Lv and Hit.
- x aims the selected ability in BOTH the spell and action menus. Self-only and party-wide casts have nothing to aim, so x tells you that and leaves the menu open (Enter still casts). Items keep x = examine.

## 1.23.0 - 2026-07-08
- Picking up things that belong to someone now asks first, like the game's own "Steal" flow (and respects the crime warning option). Applies to g, numpad0, hold-g grabbing and taking from the browser; owned items are tagged [steal] in the pickup lists. Before this, the mod's pickups silently committed the crime.
- Spell/action icons are drawn exactly the way the ability tooltip draws them: full color on the plain row, no dark square behind. The Lv / Hit / Dmg / Cost / Left header labels finally show, lined up over their columns.
- The item you hold in your hands is pinned to the TOP of every item menu with a (held) tag: pick food off the floor into your hands, press e, it leads the list.
- Holding v (or a) for manual targeting works again: it kept casting on yourself because the menu wiped the manual flag while closing. Also new: pressing x on a spell in the list aims that one cast manually right away.
- Wide Skill got its own key: shift+w lists every spell and action that can target your party; Enter casts it on all of them. w on a selected spell still does the same from inside the list.
- Movement keys no longer leak through open menus (pressing w in a spell list used to step you forward). Menu captions now list their controls: [x aim / w party / z sort] in ability lists, [z sort / x info] elsewhere.
- Sorting with z leaves the utility rows alone: [ Filter by type... ], [ Manage favorites... ], [ Take everything ] and friends stay put at the bottom (or top) instead of shuffling into the items.

## 1.22.0 - 2026-07-08
- Spell/action menus have REAL columns now: Lv / Hit / Dmg / Cost / Left, each aligned under its own header label like Elona. The icons got their elemental colors back, on a dark backing so they read as icons instead of blobs.
- numpad0 instantly picks up everything on your tile, no menu. g keeps the selection menu (with "[ Take everything ]" at the bottom), and HOLDING g opens "Grab what?": everything grabbable around you, including furniture and dropped containers, so a purse on the floor is one hold away.
- That dropped purse also shows a "Hold" action in the space/interact/tile menus now (it never appeared anywhere before).
- Quickbinding a container gives quick ACCESS: shift+digit opens it instead of putting it in your hands.
- Container and backpack settings can open the game's real "advanced distribution" panel (the one with sliders and detailed categorization), instead of just the on/off toggle.

## 1.21.1 - 2026-07-08
- Spell/action icons show their real art again (the element color was smothering them into solid blobs). The stat column width is configurable (AbilityStatWidth) and the scaling-stat icon is bigger and sits right next to the numbers.
- Hotkey casts FORCE-cast at your remembered target, friend or foe, and failures only print the game's own message (no more "stock is depleted" plus "you cannot aim there" double-talk).
- g / numpad0 opens a pick-up list when your tile has several things: choose items and counts, or "[ Take everything ]". A single item is still grabbed instantly. The confusing per-tile "pick all" entries are gone from the space menu.
- o always shows the container list now (no more auto-opening the purse), with "[ Backpack settings (autopickup)... ]" on it: which categories get picked up, refuse rotten, advanced distribution, name filter.

## 1.21.0 - 2026-07-08
- Spell/action menus cleaned up: the stats get a proper wide column (no more everything scrolling in a sliver), the scaling-stat icon is back next to the numbers, and the tooltip keeps the deep detail.
- Sorting fits the menu now: z cycles original / name / CATEGORY / STOCK in ability menus, and original / name / weight / COUNT in item menus.
- HOLD v (or a) while the menu opens = manual targeting mode: every cast opens the aiming cursor and you can target anyone, friend or foe. Tap = the quick flow as before (remembered target casts instantly).
- w on a selected spell casts it on your WHOLE party (Elona's Wide Skill), for spells that can target allies.
- The party menu (shift+q) shows each member's live sprite, with their info card on hover.

## 1.20.2 - 2026-07-08
- Page number, item count and total weight are back in the window TITLE, where they render reliably. The bottom hint line is gone (it refused to show twice); the key list lives in Mod Help.
- The space/interact/tile menus show the live sprite of each character next to their actions, like they already did for items and stations. Hovering the row shows their info card.
- The list-style crafting menu (CraftingStyle=menu) got its dressing: product sprites on every recipe, green names for craftable and gray for not, and labeled counts ("own 2  make x5" instead of naked numbers).
- Materials in the list-style menu are picked from a proper list now (sprites, counts, current one marked) instead of cycling blindly one by one; your pick is remembered for next time.
- Dropdown configs: CraftingStyle, NameDisplay and WheelStyle now offer their valid choices as a picker instead of a free-text field.

## 1.20.1 - 2026-07-06
- Escape cancels a craft in progress (nothing could stop one before; it would chew through your whole log pile). Keys can't accidentally start another craft while one is running.
- The bottom line of the menus (page number, item count, total weight, key hints with z/x) actually shows now; it silently failed to attach in every window since it was added.
- Crafting: scrolling the recipe list no longer flickers (the preview updates when the cursor rests instead of every step), and materials get their own key: m opens the material picker, m again hops to the next slot. Enter can no longer craft by accident while you're aiming at materials.

## 1.20.0 - 2026-07-06
- Crafting uses the game's normal window again, now fully keyboard-driven: up/down walk the recipes with the product panel following live, +/- set the count (shift for 10s), Enter crafts, left/right reach the material pickers (Enter opens, arrows choose), z/x switch categories. Fewer keypresses than the mouse, with all the window's info intact.
- The list-style crafting menu from 1.19 is still available: set CraftingStyle=menu in the config.

## 1.19.6 - 2026-07-06
- Space waits again: when there's nothing around you to interact with, Space passes through to the game's wait, and holding it keeps waiting, like vanilla. With something nearby it still opens the interaction menu. (Numpad 5 and left-clicking yourself always worked.)

## 1.19.5 - 2026-07-06
- Crafting menu catches up to the native window's info: recipes show their skill and how many you own next to the name (the right column is just the craftable count now, no more garbled text), hovering or selecting a recipe shows a full tooltip (skill vs yours, stamina cost and whether it will drain you, craftable count, owned, every material with have/need), and the recipe view opens with an info line (skill, stamina in red when it would hurt, owned).
- Switching crafting categories or browser inventories no longer piles up history: shift exits the window instead of replaying every tab you visited.
- Holding x (0.6s) in the crafting menu or inventory browser resets to [All] / You.

## 1.19.4 - 2026-07-06
- Fixed crafting opening a broken Elin window that Escape couldn't close (with the material panel stuck open). The keyboard recipe menu now opens the way it was meant to.

## 1.19.3 - 2026-07-06
- Fixed 1.19.2 breaking most menu features at once (arrow navigation, weights, tooltips, the bottom info line, the family bar). If the mod ever half-breaks like that again, the log now says so in one loud line.

## 1.19.2 - 2026-07-06
- Fixed: pressing Space in a menu could silently activate things and stop the arrow keys from working. Space now simply confirms, like Enter.
- Menu windows show their page, item count and total weight at the bottom (like Elona), with a key-hint line under every menu. Titles are clean again.
- Ctrl+1-9 also binds hotkeys in menus, for Elona muscle memory (plain 1-9 still works; shift+1-9 uses them).

## 1.19.1 - 2026-07-05
- Crafting menu: switch category tabs with z/x, like everywhere else.
- Every menu now lists its usable keys at the bottom (turn off with MenuKeyHints).

## 1.19.0 - 2026-07-05
- Crafting is now Elona-style: stations and quick craft open a recipe list (craftable first, with skill and how many you can make). Pick a recipe, swap each material through everything you own (including home storage), set a count, craft. The old window is still one row away.
- Inventory browser (shift+x): flip between your pack, each party member, and your home storage with z/x; take items with the count picker.
- Item processors can use materials from your home storage while you're at your base.

## 1.18.0 - 2026-07-05
- New hotkey system: press 1-9 on any item or spell in a menu to bind it, shift+digit to use it. Bindings are smart: bind a rod of identify and any rod of identify you carry will fire, including ones you pick up later. Small icons on the hotbar corners show your bindings; bound items show a [n] tag in menus. The game's own hotbar is untouched.
- The CapsLock key lock now really blocks Tab, and no longer interferes with Escape or Enter.
- Fixed the hotbar disappearing from the widget editor.
- Slimmer weight column, no more endlessly scrolling names in the equipment window, "Light" instead of "Light Source". Window and column widths are configurable.

## 1.17.1 - 2026-07-05
- Mod keys now always win over the game's keys when they clash, in every situation.
- CapsLock toggles a full vanilla-key lock: while on, only mod keys, movement and the hotbar digits do anything. No need to unbind keys in the game's settings.

## 1.17.0 - 2026-07-05
- Fixed Tab sometimes closing a menu and opening the game's inventory instead.
- Run into a tree with an axe (or a wall with a pickaxe) and you'll chop/mine it, no shift needed. Doors still just open.
- The t menu pins your held item at the top as "(held) put away".
- Family bar: size slider and hint toggle in its right-click menu.

## 1.16.1 - 2026-07-05
- Long item names scroll within their own space instead of sliding over the icons.
- Item counts in the window header count entries, not stack sizes.

## 1.16.0 - 2026-07-05
- Held items now work generically: hold anything (brush, whip, syringe, any tool), then i toward a target, Space around you, or s on yourself, and its actions are there. Works for every item in the game and other mods.
- Hold an arrow key to keep scrolling through menus and lists.
- Container settings menu now has everything the game offers: sort options, distribution priority, cooler mode, category filters, autodump, name filter.
- Inventory examine list (shift+x) with totals.

## 1.15.0 - 2026-07-05
- The second hotbar became small corner icons on the real hotbar (always aligned, always your skin).
- The family bar uses the game's real window skin, with the full style menu (background, size, color, decorations).
- Fixed Tab dying in the equipment window. Character sheet buttons navigate in reading order.
- Spell and action lists show the real ability icons plus level, cast chance, damage dice, power, cost, and casts left.

## 1.14.0 - 2026-07-05
- The window-family bar: Elona-style diamond tabs shown while item/equipment/spell windows are open. Click to jump; Tab cycles.
- The family bar and the second hotbar became real Elin widgets: toggle, drag, lock and anchor them in the widget manager; positions save with widget themes.

## 1.13.0 - 2026-07-05
- Tab cycles related windows from your current one: examine → drop → eat → read → drink → zap → tool → open → mix → throw; equipment ↔ character sheet; spells ↔ actions.
- x reopens the last item menu you used, like Elona.
- Quest boards: up/down scroll, left/right switch tabs, r rerolls.
- The z wheel picks on key RELEASE, so you can roll onto diagonals without misfires; the pending slot glows.

## 1.12.0 - 2026-07-05
- Chests and containers open the keyboard menu no matter how you open them. Item processors (mill, seed maker...) get a keyboard ingredient picker.
- s harvests or collects whatever you stand on instantly; otherwise it opens the tile menu with "Search the ground".
- c opens the character sheet. Favorites appear first in the spell/action lists.
- Targeting (*) shows the same hover info card as the mouse, so hover mods work with it.

## 1.11.x - 2026-07-04
- True in-world targeting: no windows in combat. Spells and throws fire at your remembered target instantly; otherwise a light targeting mode with a tile highlight opens. * sets the target.
- Casting respects each spell's real targeting (self, party, single target, ground).
- Digit hotkeys, spell macros (Macro1-3), and a bar showing the other hotkey page.
- Native windows (quest boards, crafting, dialogs, talk choices) navigable by keyboard.
- Fixed item icons rendering with wrong colors on some saves.

## 1.10.0 and earlier - 2026-07-03
- The core set: classic Elona keys for everything (e/q/r/Z/g/d/w/t/u/B/T/o/C/s/i/p/O/G and more), Elona-style paginated menus with letters, icons, weight column and sorting, equipment window with body slots, direction prompts that match what you see on screen, the z action circles, bump-to-talk and bump-to-use, and every key rebindable in the config.
