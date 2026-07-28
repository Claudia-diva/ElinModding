# Changelog

## 1.38.3 - 2026-07-10
- The Dig key is m alone; the shift+d spare is gone for good. Shift belongs to dashing AND to auto-repeat mods (AutoAct chains ANY task started while shift is held), so a shift-based dig bind auto-mined the map by accident. If your saved config still says shift+d (or "m, shift+d"), change Dig to m. With AutoAct installed the split becomes deliberate: m or bump-mining digs once, shift+direction with the pickaxe held starts a chain on purpose, and any fresh key or Escape still stops a running chain.

## 1.38.2 - 2026-07-10
- The Throw menu no longer forces the [recent] sort. The sort choice is one global setting that follows you between windows, so seeding it anywhere polluted everywhere; no window overrides it, ever. Your z choice is yours. The blue (new) tags still mark fresh pickups in every list, and [recent] stays available in the z cycle.

## 1.38.1 - 2026-07-10
- Bump-mining beats the game's wall avoidance. Holding a direction while WALKING to a wall never counted as a bump (only a fresh press did), so the movement assist slid you along the face instead of digging. A held key now keeps working the tile whenever you stand idle in front of it, and while the dig runs the held key holds your position instead of feeding the wall-slide. Result: hold a direction into rock with a pickaxe and you tunnel wall after wall; release, or press any OTHER direction, to stop. Bump-talk still needs a deliberate fresh press (no chat spam from held keys).

## 1.38.0 - 2026-07-10
- Category selection rows show the first matching item as their icon, so the categories read at a glance instead of as bare words.
- Recent pickups are visible now. The last few things you picked up carry a blue (new) tag in every item menu, and z gains a fifth sort: [recent], newest pickups first. The Throw menu OPENS on recent sort, so the junk you just grabbed sits on top ready to fly; z still switches to any other order. Recency counts ground pickups, container takes and ally takes alike.
- Throwing respects your marks: important-starred items and containers with things inside are not listed in Throw and refuse every other throw route too (f-spam, quickbinds). No more launching a container full of keepsakes.

## 1.37.2 - 2026-07-10
- Movement interrupts everything. A fresh direction press cancels whatever your character is doing (mining, chopping, hauling, riding) and your step goes through. Escape does the same and both now also stop auto-repeat chains (auto-act style mods requeue the next task instantly; the cancel keeps swatting them for half a second so the whole chain dies, not just one link). Bump-mining is unaffected: the direction you were already holding when the task started does not count as an interrupt.

## 1.37.1 - 2026-07-10
- Mining reworked. The Dig key is now m (shift+d stays as a spare, but it fights the dash-east movement, which is why it felt like it shoved you around). m auto-holds your mining tool; with exactly one minable wall next to you it just mines it, ore veins included; several walls ask for a direction, and refusals say WHY (no wall there / your tool cannot mine that wall).
- Bump-mining actually works now: walk into a wall (ore veins too) with your pickaxe held and mining starts, same as bump-chopping trees. Mining is flagged like a crime internally, and the bump filter mistook that for "dangerous" and refused; it now checks the zone's actual law instead, so you cannot accidentally mine a town wall by walking into it.

## 1.37.0 - 2026-07-10
- Throw spam, three ways: hold a stack of throwables (t / numpad0 on the item) and f throws ONE per press at your enemy lock, like firing a bow. Quickbinding a throwable (shift+digit) throws one directly instead of just holding it. The T menu keeps working and remembers your last item. All of it aims at the enemy lock or auto-acquires, and opens the cursor when nothing is in range.

## 1.36.1 - 2026-07-10
- Throwing from a stack throws ONE, like the vanilla throw (a stack of 10 capture balls went flying as one clump and every ball captured). Items the game itself hurls whole keep that behavior.

## 1.36.0 - 2026-07-10
- Mouse aiming is back everywhere: in targeting mode the cursor follows your mouse and left-click fires at the hovered tile (arrows, Space and Enter all still work; right-click or Escape cancels). Direction prompts take a click on any highlighted tile, your own included when allowed. Keyboard-first was never meant to be keyboard-only.
- KeyboardShops config (Menus section): set false and shopkeepers, vendors and dupe machines open the vanilla shop windows again, with all our shop handling out of the way. CapsLock pause keeps bypassing everything regardless.

## 1.35.0 - 2026-07-10
- The mod is translatable. Every message, menu title, overlay text and label goes through a translation table; grab verbs already use the game's own localized act names. Translators: copy LangMod/EN/elonakeys.tsv (inside the mod package) to LangMod/<your language code>/elonakeys.tsv and fill in the second column (english TAB translation, {0}/{1} placeholders stay). The game's language setting picks the file automatically; anything untranslated stays English.

## 1.34.2 - 2026-07-10
- The targeting texts are configurable (config > Overlay, also in the mod config screen): LockHudX/LockHudY move the lock readout from its top-center spot, AimLabelY moves the aim label up from the bottom, and the two FontSize entries resize them. Size 0 hides that text entirely. Changes apply live, no restart.

## 1.34.1 - 2026-07-10
- Bladestorm, swarm, rush and every other combat art now actually attack. They carry none of the game's usual hostile markers; the classifier now also reads the ability-type column (attack / dot / debuff), the same data the menu's category buckets use. Melee arts hit the adjacent enemy instantly, ranged ones auto-acquire, hexes go to the enemy lock.
- Abilities on cooldown SAY they are on cooldown and how many turns remain. The game refuses those casts silently, which looked like the key doing nothing.

## 1.34.0 - 2026-07-10
- The on-screen text finally uses the game's own skin font (the lock readout and the aim label were drawing with a font that renders blank on most setups, which is why you never saw them). Lock readout sits top center: enemy in red, ally in green, Swap warning in amber.
- Melee-reach abilities (bladestorm and friends) just FIGHT: a hostile cast hits the nearest enemy it can actually perform on, so pressing the key with an enemy next to you swings immediately. When no enemy is in the act's reach it says so and spends nothing, instead of opening a target selection over enemies the cast could never touch.
- Targeting mode highlights enemies on the ground exactly like the game's own "highlight enemies" setting (and stays off when you keep that setting off).
- numpad0 moves items without the submenu dance: on a chest's item it takes the whole stack straight to your backpack; on your own items in the browser (shift+x) it STORES them wherever your containers' distribution settings say (priority, filters, the Share toggle decide the chest). Holding items in hands stays on q/e/r/t/E and the verb menus.

## 1.33.0 - 2026-07-10
- Attack spells are recognized properly now. The lock routing was reading a flag the game never sets on spells; it now uses the game's own negative-ability marker (the same one that colors those abilities red in your ability window). Attacks and hexes go to the enemy lock or auto-acquire, buffs and heals go to your ally lock (yourself included), and casting one never re-aims the other. The shift-row quickbinds route by the same rule whether they hold buffs or attacks.
- f repeats your last ATTACK, not your last cast: buff yourself mid-fight and f still shoots the enemy. With no attack cast yet, f falls back to native fire.
- Your locks are on screen now: a readout at the top center shows the current enemy lock, ally lock, and a warning while Swap is armed, updating live. The aim cursor keeps its key hints at the bottom of the screen.

## 1.32.0 - 2026-07-10
- Target locks, final form. Enemies and allies lock separately with * and both hold at once; yourself is a valid ally lock. Hostile casts, f, bows and throws ALWAYS use the enemy lock (or auto-acquire the nearest foe). Buffs and heals ALWAYS use the ally lock. Neither side ever crosses over on its own: buff your girl while shooting her attacker, or solo-buff yourself between volleys, without touching the target key.
- numpad / (or ;) = Swap: the next cast aims at the opposite lock, for hexing your own ally or healing an enemy on purpose. Tap to arm, tap again to cancel, or hold it while casting.
- Shift no longer re-aims casts. shift+1-9 quickbinds fire at their natural target (buffs at your ally lock, attacks at your enemy lock), no surprises.

## 1.31.0 - 2026-07-10
- Attack casting and the ally lock are properly separated: hostile spells never fall back to the ally lock or to yourself, f repeats attacks at enemies only, and the aim cursor for a hostile cast starts on a foe instead of whoever you buffed last. Locking yourself as your own ally is ignored. Touch interactions keep working freely on your people.
- The aim cursor no longer eats the same Enter press that selected the spell, so the choice actually confirms when YOU press it.
- numpad0 on a plain menu row holds that item in your hands, same as in the browser: q, hover a potion, 0, and it is armed for throwing. No digging through the inventory.
- The grab menu speaks vanilla now: "Pick up" (into the backpack), "Grab" (into your hands), "Grab as furniture" (in hands, Space places it at your feet). The furniture option appears for potions, tools and everything else the game itself offers it for.

## 1.30.0 - 2026-07-10
- Escape closes EVERY open window at once and takes you straight back to the map; shift stays the one-step back key. Conversations and yes/no prompts keep their normal Escape.
- Touch-range abilities (interactions included) finally aim like everything else: with a target locked and in reach they fire instantly, and the direction prompt now targets whoever stands on that tile instead of refusing. Use them on your allies freely.
- The two grab flavors are spelled out in the hold-g menu: "Grab (carry)" lifts furniture into your hands so Space can place it at your feet, "Take (to backpack)" pockets it directly. Loose holdable items also offer "Hold (place / throw)".
- Furniture carried with hold-g now actually offers "Place" on Space (the carry never registered as a proper hold before, so only put-away and drop showed).
- Fixed the hold-g window re-collecting the same item when you kept pressing Enter: taken items go inert immediately, and the pickup windows refresh their list live after every take.

## 1.29.1 - 2026-07-10
- TWO TARGET LOCKS: * on an enemy sets your enemy lock, * on an ally sets a separate ally lock, and both hold at once. Attacks and debuffs fire at the enemy lock (or auto-acquire the nearest visible enemy when none is set) and never touch the ally lock; heals and buffs go to the ally lock instantly. Hold SHIFT while casting to deliberately aim anything, harmful included, at your locked ally. The message tells you which lock you set.
- f repeats your LAST cast spell/action at its usual target when no ranged weapon is equipped (ranged still fires natively). Spam away.
- Space menu cleaned: held-item "Place" appears once, for YOUR tile (place at your feet); directional placing stays on i. No more eight anonymous Place rows.
- The hold message only promises i/Space/s when the item really has such actions; scrolls and potions say just "Holding X" (read/quaff them with r/q, they sit pinned on top).
- Tab-cycling fixed for real: a skipped stop was throwing off the count and jumping past the next window too (the log proved Tool was never tried).
- Advanced distribution is parked as TODO: the row is grayed and tells you the workaround (CapsLock pause + the vanilla chest's gear button). Its keyboard version comes with the theming update.

## 1.29.0 - 2026-07-10
- shift+q "Items" opens the same two-way trade Give uses (their items / yours, Tab flips sides), so party inventories finally move both ways from either door.
- Containers with things still inside can no longer be put into other containers, shipped, delivered, sold or handed to allies (the game's own rule; our menus were sidestepping it).
- Home stock in the browser (shift+x) lists your CONTAINERS: open one for full take/put/settings, so things go back into the chest they came from. The old everything-in-one-list view is the top row if you want it.
- Counts formatted properly: [Inv 33/35] [Worn 17/20] [Wgt 45.2/58.0s], with worn gear shown against your body slots instead of inflating the inventory number.
- numpad0 on your items in the browser takes them into your hands (holdable, unequipped items only). numpad* (the Target key) toggles the important mark on any selected item, Elona style; the gold star updates everywhere.
- Advanced distribution: per request, the row now just opens the vanilla container window; the real slider panel lives behind its gear button (mouse). Every keyboard host the game offers kills the panel on its own.
- Boards: z = previous tab, x = next tab, r = reroll (the x-reroll experiment is gone; directions are consistent across every menu).
- The family-Tab skip only jumps stops that genuinely have nothing to show, and logs each skip when DebugLog is on (a stop can look "skipped" if all its items are equipped or held).
- PageSound config: pick any game sound for page turns, or 'none'. Full window theming (skins, text colors) is on the roadmap.

## 1.28.5 - 2026-07-10
- Menus remember your cursor: reopening Drop/Cast/anything returns to the page and row you left. Pages turn with the paper rustle.
- Space and s list what you're STANDING ON and nearby things first; your mount and hosted pets moved to the bottom.
- Spell icons that rendered as black discs (death funnel...) show their real art: their colors are encoded for the game's icon shader, the same fix item sprites needed long ago.
- shift+q party inventories: Tab (or period/comma) hops between members' bags; slot counts show carried items and worn equipment separately everywhere (no more "52/35").
- Give: party members and pets get the two-way trade window (Tab flips give/take); gifting outsiders stays one-way.
- Favorited/important items can no longer be put into shipping chests, delivery boxes or the tax box.
- Tab-cycling into an empty menu (Zap with no rods, Read with no scrolls) skips to the next window instead of killing the chain.
- The advanced distribution panel opens in its own centered overlay (Escape or click outside to close); the popup-menu host it used before self-closes without mouse-over, which is why it kept showing nothing.
- Request boards show their controls in the title bar.

## 1.28.4 - 2026-07-10
- Request boards scroll with up/down for real now: their rows are a composite type the navigator never recognized as buttons. x on a board rerolls (same as r); z alone covers the two tabs.
- The advanced distribution panel shows for real now: popup menus self-close the instant the mouse isn't over them, which for a keyboard user is immediately. It opens centered and stays until Escape.
- Teleporting (or any zone change) while a shop/service window was open could leave an invisible window in the game's reuse pool, causing flicker and windows that open blank. Hidden windows now restore themselves no matter how they get closed.

## 1.28.3 - 2026-07-09
- Inventory browser (shift+x): period/comma switch between You / party members / Home stock, so z is sorting again, x examines again (they had been hijacked as the switching keys, which also froze whatever sort you came in with), and Tab keeps hopping the window family.
- numpad0 on a stack row (or inside the spinner) acts with the WHOLE stack instantly: drop all, take all, no prompt. Undroppable and important-marked items stay protected by the game's own rules.
- Items marked as important show a gold star in every menu (the mark used to be invisible here).
- The open-container list (o) shows each bag's weight and used/total slots and sorts properly (z: weight or item count); the utility rows stay pinned out of the sort.

## 1.28.2 - 2026-07-09
- FIXED: dropping a chosen amount dropped the whole stack (the game's own drop function ignores the count; we split first now).
- FIXED: the "[ Advanced distribution panel... ]" row closed every window and showed nothing (wrong menu host; the game has two and one of them closes all layers). The panel opens for real now.
- That same failure could leave an invisible leftover menu that silently killed keyboard navigation in every native window afterwards (request board arrows, home board Enter, resident board). The mod now detects and clears that state on its own. If your boards felt dead, this was why.
- Shift closes native windows too now: request/home/resident boards, dialogs, the character sheet, like it always closed the mod's menus. (Talk scenes are exempt.)
- Enter on a yes/no popup takes the first (default) answer without arrowing to it first.
- Party inventories (shift+q) show the member's used/total slots in the footer.

## 1.28.1 - 2026-07-09
- The backpack-space check really covers every take now (container Take and party-member take had slipped through), and a pick-an-item window that fails to open cleans up after itself.

## 1.28.0 - 2026-07-09
- EVERY "pick an item" window is an ElonaKeys menu now: altar offerings, dip/blend, identify/uncurse/enchant/material scrolls, wizard services, bank deposits, the tax box, harvest-quest chests, gacha, the recycle bin, the gene machine, the mod kit, refueling. One list, Enter uses the item, paid services charge their normal fee. (Bank withdrawals were already keyboard dialogs; now deposits match.)
- CapsLock pause is airtight: while paused, nothing of the mod runs at all (a few fences and the Escape action-cancel used to slip through), and the movement keys can't stay stuck if you pause mid-trade.
- z and x no longer double-fire (window tab switch AND wheel/last-menu) when a vanilla inventory window has keyboard focus.
- Holding v to arm manual targeting, then closing the menu without casting, no longer leaves the next hotkey cast aiming manually.
- Every "take" path (containers, party members, browser, pickup) refuses politely when your backpack is full instead of half-working.
- A shop that fails to open can no longer leave an invisible window behind.
- In-game help fully rewritten: current shop/trading/pick-item flows, the real CapsLock behavior, the full column list, plain-text symbols the game font can render, and a catch-up changelog. README matches.

## 1.27.2 - 2026-07-09
- Home-resource boards (shops that spend your base's resources instead of money) are fully keyboard now too, no exceptions left: same direct transactions, and the footer shows the resource and its stock instead of money.

## 1.27.1 - 2026-07-09
- Shopping needs NO windows but ours anymore. Buying and selling run as direct transactions (same prices, money, space checks, sell-identify and buy-back rules as the vanilla flow), so "Cannot reach that item's window" is gone, items inside pouches sell fine, and the mod no longer opens a backpack pane at all.
- Stacks get the ElonaKeys count spinner in the shop (Enter on a stack, left/right sets how many, Enter confirms) instead of the vanilla amount popup.

## 1.27.0 - 2026-07-09
- YOUR WINDOW LAYOUTS ARE SAFE. The shop no longer moves or closes ANY window you had open; only the shop's own window (and a backpack pane if the mod itself opened one) is made invisible in place, and visibility is restored on close. Window positions are never touched again.
- Trading with party members and pets opens an ElonaKeys two-pane menu: their items / your items, Tab switches sides, count spinner moves things either way, both carry loads in the footer. No vanilla window.
- Delivery boxes and shipping chests take items FOR REAL now: the game secretly routes their contents into a hidden shared container (the chest itself always stays empty, which is why every earlier fix bounced). The menu now opens and fills that real container, so shipping and quest deliveries work.

## 1.26.5 - 2026-07-09
- Shipping chests, delivery boxes and other special containers accept items from the keyboard menus for real this time. They report themselves "full" through every normal check (that's how the game stops them acting as regular storage; the mouse only works because dragging onto a slot skips the check), so the menu now judges them by actual used slots vs capacity.

## 1.26.4 - 2026-07-09
- Shop polish, per report: TAB switches buy/sell (as originally specced; z is sorting again and x examines again). Prices show in the vendor's real currency ("4,320 orens", not "moneys"), the column header says Price, and the item's weight rides in the name line. Anything bought this visit shows its BUY-BACK price in the sell view (what you paid, like the vanilla window), and your money in the footer updates after every transaction.

## 1.26.3 - 2026-07-09
- The shop list window REALLY shows now. The game finishes attaching its shop window a moment after creating it, so ours was opening underneath it and the vanilla window landed back on screen over the hidden spot. Everything now happens right after the game finishes: vanilla windows leave the screen, the ElonaKeys item list opens on top.

## 1.26.2 - 2026-07-08
- Attr column in the spell/action lists (the scaling attribute: MAG, WIL, CHA...), between Lv and Pow, with its header. The attribute text left the name line, so names are cleaner. AbilityColumns config accepts "attr".
- Aimed casts can finally hit ALLIES with hostile spells/abilities (hold-a or x on the row, and quick-casts at a remembered ally): enemy-only targeting is bypassed while YOU aim, classic Elona rules. The AI and normal mouse aiming are untouched.

## 1.26.1 - 2026-07-08
- The shop is ONLY the ElonaKeys window now, like Drop and Wear. The vanilla shop and backpack windows never show (they exist off-screen because the game's own transaction machinery lives in them); closing the menu closes the whole trade.
- Spell/action icons are BACK. The previous icon experiment drew nothing at all in menu rows; icons render the way the action list looked before.

## 1.26.0 - 2026-07-08
- THE SHOP WINDOW IS OURS NOW. Talking to any vendor opens an ElonaKeys menu: the stock with prices and letters, Enter buys (stack popups included), z/x flips to the SELL view of your bag with what they'd pay, the footer shows your money. All of it still runs through the game's own transaction, so prices, space checks and karma are untouched. Works for every vendor, duplication shops included.
- Buy-amount popups are keyboard-navigable while the shop menu is open.
- Closing the menu (shift/Escape) leaves the native shop window for mouse use; talk to the vendor again for the menu.

## 1.25.2 - 2026-07-08
- FIXED: opening a shop threw endless errors. The backpack auto-open used a leftover game function that creates a half-initialized window; it now opens your inventory through the exact same path the Tab key uses. If you hit the errors, reopening the game clears the broken window.

## 1.25.1 - 2026-07-08
- Trading actually works now (1.25.0 searched for the shop windows in the wrong place and found none, silently). Same keys as advertised: arrows, Tab between panes, Enter to buy/sell, x examine. It's one generic system for every vendor; nothing is per-shop.
- CAPSLOCK IS THE ESCAPE HATCH now, repurposed: press it and the mod pauses COMPLETELY: native windows, native keys, no redirects, until you press it again. Any time a mod window misbehaves, CapsLock gets you the normal Elin one.
- Shipping chests and delivery boxes accept items again ("full at 0/40": the game reports special containers as always-full through the simple check; we now ask the same question the native drag does).
- The bright plate behind ability icons is gone (that was my attempt at the fried-sprite fix, wrong call). Spell icons now use the tooltip's actual icon component, cloned from the same prefab, so whatever the tooltip renders is what the list renders.
- Empty hands (shift+e) now really puts toolbelt tools away (shears, sickle, hammer): the toolbar kept them selected and re-equipped them instantly; we now deselect the way the toolbar itself does.
- Container settings menu cleaned up: all values in plain readable text, and Distribution priority is a simple "Enter cycles -5..20" instead of the confusing spinner numbers.
- Party member inventories show their free space in the footer ("slots 14/25").

## 1.25.0 - 2026-07-08
- TRADING BY KEYBOARD. Open any shop and your backpack opens beside it: arrows walk the item grids, Tab (or left/right) hops between the shop's stock and your inventory, Enter buys or sells exactly like a right-click (same prices, same confirmations, same stack dialogs), x examines. The game's own rules still apply: no money = no sale, no space = "backpack is full", purchases stack and store like a normal pickup.
- This covers every window of that kind with the same keys: shopkeepers, the kettle duplication shop, Demitas's spell duplication, spell vendors, wandering merchants. Bank and trainers talk through dialog choices, which were already keyboard-driven.
- Every inventory window's title shows its slots: "Backpack [23/40]".

## 1.24.3 - 2026-07-08
- FIXED: dropping/giving the last item on a menu's last page left the window stuck on a page that no longer existed ("None", arrows dead).
- FIXED: Escape opens the system menu again. The new "Escape cancels your action" feature considered the idle state a running action and swallowed every press.
- Spell icons draw the exact same way the tooltip draws them; the row's built-in icon slot was quietly recoloring them (white action icons hid it, colored spell icons showed it).
- Ability icons sit on a pale parchment plate like the tooltip's. Window skins are saved per save file, and on a dark skin the bare colored glyphs turned to mud, which is why one save looked deep-fried and the other didn't.

## 1.24.2 - 2026-07-08
- The column headers were RENDERING at last, just in the wrong place: the window's own layout system grabbed them and stacked them at the bottom (that vertical Lv/Pow/Hit/Dmg/Cost/Left pile in the corner). They're exempt from the window layout now and sit in the header strip next to "Name", over their columns.
- Footer layout per spec: item count / weight / container slots CENTERED, the page number at the bottom RIGHT, both in the same font as the menu rows.
- Item and equipment windows get a "Weight" header over the weight column, so every window has labeled columns, not just the ability lists.
- Abilities whose scaling stat has no icon (piety...) no longer draw a white square in the stat slot.

## 1.24.1 - 2026-07-08
- Column headers, fourth try, different foundation: they now anchor to the first ROW's actual on-screen position and use its font. (The previous anchor, the window's sort button, turned out not to exist on these windows at all; the log finally said so.) Column ORDER is configurable: AbilityColumns in the config, reorder or remove entries.
- The menus grew an Elona-style bottom line: page, item count, total weight, and for containers the SLOTS (used/free), built on the same row-anchored technique.
- Escape cancels whatever your character is doing: eating, processing, stealing, harvesting, crafting. Anything with a progress bar stops on Escape when no window is open.
- Sorting by weight/count in the category view sorts the CATEGORIES now (by their total weight / total pieces), each category shows its combined weight, and "All items" stays pinned on top.
- Wide (shift+w) lists only what can truly multicast on your party: attack spells and hexes are out (the game aims those at ALL ENEMIES, which is why it said "Nothing seems to happen" in town). Low spell stock now says "not enough for the whole party" instead of silently casting on just you.
- Targeting with * (or confirming any aimed cast) now sets the game's own target too: the native target marker appears on them, and ranged auto-fire respects it.
- Tab from the equipment window reaches the character sheet again: one Tab press was cycling TWICE (two handlers saw the same key), landing you back where you started.

## 1.24.0 - 2026-07-08
- Casting at a refused target no longer EATS the spell cost (the game charged first and checked after; now we check first). Aimed casts can hex/debuff your own party members without holding shift, and truly enemy-only spells tell you "(No cost spent.)" instead of failing silently.
- When your remembered target can't take the spell (an ally for an enemy-only attack, out of reach...), the cast opens the aiming cursor instead of firing blind, and the big label at the bottom names the spell you're aiming, with (party)/(hostile) tags on whoever the cursor is over.
- Spell icons draw with their real art again (they were all falling back to the same default glyph, which tinted into identical blobs; actions were unaffected). Damage dice numbers use the normal text color instead of the barely-readable tan.
- The verb keys that share letters with movement (s, w, a, d) can't step your character anymore, even if you hold them a beat too long. That's what "tapped" collectables once and walked you off them.
- "Collect", mining and other ground actions show the map object's sprite in the space/tile menus instead of a blank slot.
- shift+e empties your hands: puts away a held item or tool, grabbed furniture, or a hotbar selection that got stuck.
- The character sheet tabs (skills, traits, strategy...) scroll to keep the keyboard selection in view.

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
