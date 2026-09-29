using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ElonaKeys
{
	[BepInPlugin(Guid, Name, Version)]
	public class Plugin : BaseUnityPlugin
	{
		public const string Guid = "elin.elonakeys";
		public const string Name = "Elona Keys";
		public const string Version = "1.39.1";

		internal static ManualLogSource Log;

		public class Entry
		{
			public string name;
			public List<Keybind> binds;
			public Action run;       // custom handler
			public EAction action;   // or native EAction passthrough

			public bool IsDown()
			{
				foreach (Keybind b in binds)
				{
					if (b.IsDown())
					{
						return true;
					}
				}
				return false;
			}
		}

		private static readonly List<Entry> entries = new List<Entry>();
		private static readonly List<(ConfigEntry<string> cfg, string name, Action run, EAction action)> defs =
			new List<(ConfigEntry<string>, string, Action, EAction)>();

		private static ConfigEntry<bool> overrideGameKeys;
		private static ConfigEntry<bool> debugLog;
		private static ConfigEntry<bool> menuCategories;
		private static ConfigEntry<int> categoryThreshold;
		private static ConfigEntry<int> menuPageSize;
		private static ConfigEntry<string> nameDisplay;
		private static ConfigEntry<string> activateHotkeys;
		private static ConfigEntry<bool> shiftDirTool;
		private static ConfigEntry<bool> bumpToTalk;
		private static ConfigEntry<bool> bumpToUse;
		private static ConfigEntry<bool> isoAim;
		private static ConfigEntry<string> neutralLock;
		private static ConfigEntry<string> suppressNativeKeys;
		private static ConfigEntry<string> suppressAllKey;
		private static ConfigEntry<string> wheelStyle;
		private static ConfigEntry<string> wheelInfo;
		private static ConfigEntry<string> wheelAction;
		private static ConfigEntry<string> wheelEtc;
		private static ConfigEntry<string> wheelItems;
		private static ConfigEntry<string> wheelMisc;
		private static ConfigEntry<float> wheelPosX;
		private static ConfigEntry<float> wheelPosY;
		private static ConfigEntry<bool> shiftBarVisible;
		private static ConfigEntry<bool> familyBarVisible;
		private static ConfigEntry<float> familyBarScale;
		private static ConfigEntry<bool> familyBarHint;
		private static ConfigEntry<string> abilityColumns;
		private static ConfigEntry<string> pageSound;
		private static ConfigEntry<int> lockHudX;
		private static ConfigEntry<int> lockHudY;
		private static ConfigEntry<int> lockHudSize;
		private static ConfigEntry<int> aimLabelY;
		private static ConfigEntry<int> aimLabelSize;
		private static ConfigEntry<int> weightWidth;
		private static ConfigEntry<int> menuWidth;
		private static ConfigEntry<int> nameScrollSlack;
		private static ConfigEntry<string> quickbinds;
		private static ConfigEntry<bool> keyboardContainers;
		private static ConfigEntry<bool> keyboardShops;
		private static ConfigEntry<bool> keyboardCrafters;
		private static ConfigEntry<string> craftingStyle;
		private static ConfigEntry<string> macro1;
		private static ConfigEntry<string> macro2;
		private static ConfigEntry<string> macro3;

		internal static bool OverrideGameKeys => overrideGameKeys.Value;
		internal static bool DebugLog => debugLog.Value;
		internal static bool MenuCategories => menuCategories.Value;
		internal static int CategoryThreshold => categoryThreshold.Value;
		internal static int MenuPageSize => menuPageSize.Value;
		internal static string NameDisplay => nameDisplay.Value;
		internal static string ActivateHotkeys => activateHotkeys.Value;
		internal static bool ShiftDirTool => shiftDirTool.Value;
		internal static bool BumpToTalk => bumpToTalk.Value;
		internal static bool BumpToUse => bumpToUse.Value;
		internal static bool IsoAim => isoAim.Value;
		internal static string NeutralLockMode
		{
			get
			{
				string v = neutralLock != null ? neutralLock.Value : "smart";
				v = (v ?? "smart").Trim().ToLowerInvariant();
				return (v == "all" || v == "ally") ? v : "smart";
			}
		}
		internal static string WheelStyle => wheelStyle.Value;
		internal static string WheelInfo => wheelInfo.Value;
		internal static string WheelAction => wheelAction.Value;
		internal static string WheelEtc => wheelEtc.Value;
		internal static string WheelItems => wheelItems.Value;
		internal static string WheelMisc => wheelMisc.Value;
		internal static float WheelPosX => wheelPosX.Value;
		internal static float WheelPosY => wheelPosY.Value;

		internal static bool ShiftBarVisible => shiftBarVisible.Value;
		internal static bool FamilyBarVisible => familyBarVisible.Value;
		internal static float FamilyBarScale
		{
			get { return Mathf.Clamp(familyBarScale.Value, 0.4f, 1.5f); }
			set { familyBarScale.Value = Mathf.Clamp(value, 0.4f, 1.5f); }
		}
		internal static bool FamilyBarHint
		{
			get { return familyBarHint.Value; }
			set { familyBarHint.Value = value; }
		}
		internal static string AbilityColumns => abilityColumns != null ? abilityColumns.Value : "";
		internal static string PageSound => pageSound != null ? pageSound.Value : "paper";
		internal static int LockHudX => lockHudX != null ? lockHudX.Value : 0;
		internal static int LockHudY => lockHudY != null ? lockHudY.Value : 40;
		internal static int LockHudSize => lockHudSize != null ? Mathf.Clamp(lockHudSize.Value, 0, 48) : 17;
		internal static int AimLabelY => aimLabelY != null ? aimLabelY.Value : 130;
		internal static int AimLabelSize => aimLabelSize != null ? Mathf.Clamp(aimLabelSize.Value, 0, 48) : 20;
		internal static float WeightWidth => Mathf.Clamp(weightWidth.Value, 30, 160);
		internal static float MenuWidth => Mathf.Clamp(menuWidth.Value, 480, 1100);
		internal static float NameScrollSlack => Mathf.Clamp(nameScrollSlack.Value, 0, 100);

		internal static void SaveQuickbinds(string v)
		{
			quickbinds.Value = v;
		}
		internal static bool KeyboardContainers => keyboardContainers.Value;
		internal static bool KeyboardShops => keyboardShops != null ? keyboardShops.Value : true;
		internal static bool KeyboardCrafters => keyboardCrafters.Value;
		internal static string CraftingStyle => craftingStyle.Value;
		internal static string Macro1 => macro1.Value;
		internal static string Macro2 => macro2.Value;
		internal static string Macro3 => macro3.Value;

		internal static void SaveWheelPos(float x, float y)
		{
			wheelPosX.Value = x;
			wheelPosY.Value = y;
		}

		internal static List<Keybind> SuppressBinds { get; private set; } = new List<Keybind>();
		internal static List<Keybind> SuppressAllBinds { get; private set; } = new List<Keybind>();

		private void Awake()
		{
			Log = Logger;
			overrideGameKeys = Config.Bind("General", "OverrideGameKeys", true,
				"Who wins when a mod key collides with an Elin key.\ntrue = the mod key fires.\nfalse = the game keeps the key and the mod bind is ignored.");
			debugLog = Config.Bind("General", "DebugLog", false,
				"Verbose troubleshooting log.\nRecords why a key press was ignored and how each cast classifies and targets.\nTurn this on before reporting a problem.");
			menuCategories = Config.Bind("Menus", "CategoryFilter", true,
				"Show a category picker first when an item menu would be long.\nShorter lists (under CategoryThreshold) open directly.");
			categoryThreshold = Config.Bind("Menus", "CategoryThreshold", 25,
				"Item count above which the category picker kicks in.\nOnly used while CategoryFilter is on.");
			nameDisplay = Config.Bind("Menus", "NameDisplay", "pingpong",
				new ConfigDescription("How item names too long for their column are shown.\npingpong = scroll back and forth\ntruncate = cut them short\nwrap = break onto a second line",
					new AcceptableValueList<string>("pingpong", "truncate", "wrap")));
			menuPageSize = Config.Bind("Menus", "MenuPageSize", 15,
				"Rows per menu page.\nThe rest go to further pages; left/right arrows switch pages.");
			shiftDirTool = Config.Bind("General", "ShiftDirTool", true,
				"shift+direction uses your held tool on that tile (mine, chop, till...), like a right-click drag.\nOff = shift+direction only dashes.");
			bumpToTalk = Config.Bind("General", "BumpToTalk", true,
				"Walking into a friendly NPC talks to them, Elona style.\nParty members still swap places instead.");
			bumpToUse = Config.Bind("General", "BumpToUse", true,
				"Walking into a solid interactable uses it: quest board, gacha, fountain...\nThe Elona walk-over + Space shortcut.");
			isoAim = Config.Bind("General", "IsoAim", true,
				"Which way the arrow keys point in direction prompts.\ntrue = the tile that LOOKS that way on screen (Elin's grid is rotated 45 degrees).\nfalse = raw grid directions, matching the movement keys.");
			neutralLock = Config.Bind("General", "NeutralLock", "smart",
				new ConfigDescription("What a * lock on a NEUTRAL creature feeds: a little sister, shopkeeper, wildlife.\nsmart = throws and heals aim at it; attack spells still pick real enemies.\nall = every act aims at it, classic Elona *.\nally = pre-1.39 sorting; neutrals lock on the ally side and throws ignore them.",
					new AcceptableValueList<string>("smart", "all", "ally")));
			suppressNativeKeys = Config.Bind("General", "SuppressNativeKeys", "",
				"Keys hidden from the game entirely, comma-separated.\nFor when a native Elin key annoys you and you do not want the mod bind either.");
			suppressAllKey = Config.Bind("General", "SuppressAllKey", "capsLock",
				"PAUSE toggle for the whole mod.\nWhile ON: no mod keys, no keyboard menus, no window redirects, no trade panes.\nPure native Elin, the escape hatch when a mod window misbehaves.\nCapsLock is unused by the game. 'none' disables the toggle.");
			wheelStyle = Config.Bind("Menus", "WheelStyle", "radial",
				new ConfigDescription("Shape of the z action wheel.\nradial = classic Elona circles.\nlist = a plain window.",
					new AcceptableValueList<string>("radial", "list")));
			wheelInfo = Config.Bind("Menus", "WheelInfo", "Help,Chara,Journal,Log,Equip,Inventory",
				"Verbs on the Info ring, max 6 (the east/west slots are ring links).\nAvailable: Eat, Quaff, Read, Zap, Cast, Equip, Tool, Use, Blend, Throw,\nAmmo, Get, Drop, Open, Door, Bash, Dig, Search, Interact, Pray, Offer,\nGive, Party, Inventory, Help, Chara, Journal, Log, Fire.");
			wheelAction = Config.Bind("Menus", "WheelAction", "Cast,Fire,Get,Search,Ammo,Use",
				"Verbs on the Action ring, max 6.\nSame verb list as WheelInfo.");
			wheelEtc = Config.Bind("Menus", "WheelEtc", "Pray,Interact,Dig,Bash,Offer,Give",
				"Verbs on the Etc ring, max 6.\nSame verb list as WheelInfo.");
			wheelItems = Config.Bind("Menus", "WheelItems", "Eat,Quaff,Read,Zap,Drop,Throw",
				"Verbs on the Items ring, max 6.\nSame verb list as WheelInfo.");
			wheelMisc = Config.Bind("Menus", "WheelMisc", "Tool,Blend,Door,Open,Party",
				"Verbs on the Misc ring, max 6.\nSame verb list as WheelInfo.\nLeave empty to remove the ring.");
			wheelPosX = Config.Bind("Menus", "WheelPosX", 0f,
				"Horizontal offset of the wheel widget from screen center, in pixels.\nSaved automatically when you drag the center diamond.");
			wheelPosY = Config.Bind("Menus", "WheelPosY", 0f,
				"Vertical offset of the wheel widget from screen center, in pixels.\nSaved automatically when you drag the center diamond.");
			activateHotkeys = Config.Bind("Hotkeys", "ActivateHotkeys", "shift",
				"Modifier that fires the ElonaKeys quickbind slots.\nshift = shift+1..9 (plain digits stay native quickslots).\noff = no quickbinds.\nTo bind: select an item or spell in any mod menu and press 1-9.\nBinds resolve by kind, so a bound rod of identify fires ANY such rod you carry.");
			quickbinds = Config.Bind("Hotkeys", "Quickbinds", "",
				"Quickbind storage (slot:kind:id:name;...).\nWritten automatically; clear it to reset every slot.");
			pageSound = Config.Bind("Menus", "PageSound", "paper",
				"Sound played when a menu page turns.\nAny game SE id (paper, pop_paper, click...), or 'none' to silence it.");
			lockHudX = Config.Bind("Overlay", "LockHudX", 0,
				"Horizontal offset of the target-lock readout, in pixels from screen center.\nNegative moves it left.");
			lockHudY = Config.Bind("Overlay", "LockHudY", 40,
				"Distance of the target-lock readout from the TOP edge, in pixels.");
			lockHudSize = Config.Bind("Overlay", "LockHudFontSize", 17,
				"Font size of the target-lock readout: enemy/ally locks and the Swap warning.\n0 hides it entirely.");
			aimLabelY = Config.Bind("Overlay", "AimLabelY", 130,
				"Distance of the aim-mode label from the BOTTOM edge, in pixels.\nThe label shows target info and the key hints.");
			aimLabelSize = Config.Bind("Overlay", "AimLabelFontSize", 20,
				"Font size of the aim-mode label.\n0 hides it; the aim cursor itself stays.");
			abilityColumns = Config.Bind("Menus", "AbilityColumns", "lv,attr,pow,hit,dmg,cost,left",
				"Order of the spell/action list columns, comma-separated.\nReorder or REMOVE entries, e.g. 'lv,dmg,cost,left'.\nValid: lv, attr, pow, hit, dmg, cost, left.");
			weightWidth = Config.Bind("Menus", "WeightWidth", 48,
				"Width of the right-aligned weight column, in pixels (48 fits about 4 digits).\nLonger weights scroll inside it.");
			menuWidth = Config.Bind("Menus", "MenuWidth", 680,
				"Width of the mod's menu windows, in pixels.");
			nameScrollSlack = Config.Bind("Menus", "NameScrollSlack", 20,
				"Names that overflow their column by fewer pixels than this render as-is\ninstead of scrolling.");
			shiftBarVisible = Config.Bind("Hotkeys", "ShiftBarVisible", true,
				"Show the OTHER quickslot page as small corner icons on the real hotbar.\nThat page is what shift+1..9 fires.");
			familyBarVisible = Config.Bind("Menus", "FamilyBarVisible", true,
				"Whether the window-family bar starts enabled the first time.\nElona-style diamond tabs shown while item/chara/spell windows are open; Tab cycles them.\nIt is a real Elin widget, so the widget manager can toggle it later.");
			familyBarScale = Config.Bind("Menus", "FamilyBarScale", 0.75f,
				"Size of the family bar, 0.4 to 1.5.\nAlso adjustable from the bar's right-click menu.");
			familyBarHint = Config.Bind("Menus", "FamilyBarHint", true,
				"Show the 'Tab [change]' hint line under the family bar.");
			keyboardContainers = Config.Bind("Menus", "KeyboardContainers", true,
				"Chests and containers open the keyboard take/put menu instead of Elin's window,\nfrom any route: bumping into them, Space, or the mouse.\nAlso gates ally trading and every pick-an-item flow: offerings, dip/blend,\neffect scrolls, wizard services, bank deposit, tax box, quest chests, gacha,\nrecycle, gene machine, mod kit, refuel.\nLocked chests and surprise packages stay native.");
			keyboardShops = Config.Bind("Menus", "KeyboardShops", true,
				"Shopkeepers, vendors and dupe machines open the ElonaKeys shop window\n(keyboard buy/sell).\nfalse = the vanilla shop windows.\nThe CapsLock pause bypasses this too.");
			craftingStyle = Config.Bind("Menus", "CraftingStyle", "native",
				new ConfigDescription("Which crafting window you get.\nnative = the game's own window, driven by the keyboard (arrows pick recipes\nwith live preview, +/- count, m = materials, Enter crafts).\nmenu = the mod's list-style menu instead.",
					new AcceptableValueList<string>("native", "menu")));
			keyboardCrafters = Config.Bind("Menus", "KeyboardCrafters", true,
				"Item processors (mill, seed maker, grindstone, cooking tools...) open a\nkeyboard ingredient picker over the native window.\nPicking ingredients starts the craft; Esc leaves the window to the mouse.");
			macro1 = Config.Bind("Macros", "Macro1", "",
				"Ability aliases cast in sequence, comma-separated.\nEach spell goes to its usual target (self/party per spell; targeted spells\nhit your current enemy).\nFind aliases in the spell list names or the ability window.");
			macro2 = Config.Bind("Macros", "Macro2", "",
				"Second macro sequence.\nSame format as Macro1.");
			macro3 = Config.Bind("Macros", "Macro3", "",
				"Third macro sequence.\nSame format as Macro1.");

			// Defaults follow classic Elona (elona.fandom.com/wiki/Controls).
			Def("GetAll", "keypad0", Handlers.GetAll, "Instantly pick up EVERYTHING on your tile, no menu."); // before Get: wins the shared key in old configs
			Def("Get", "g", Handlers.Get, "Pick up: one item goes instantly, several open a selection menu.\nHOLD the key to grab anything nearby, furniture and dropped containers included.");
			Def("Drop", "d", Handlers.Drop, "Drop menu with a count spinner.\nEnter on a stack, left/right adjusts the count, Enter drops.\nThe menu stays open.");
			Def("Eat", "e", Handlers.Eat, "Eat something from your inventory.");
			Def("Quaff", "q", Handlers.Quaff, "Drink a potion, or from an adjacent well/fountain.");
			Def("Read", "r", Handlers.ReadItem, "Read a book, scroll or scrap.");
			Def("Zap", "shift+z", Handlers.Zap, "Zap a rod, then pick a direction (wait key = yourself).");
			Def("Cast", "v", Handlers.Cast, "Spell list, paid in mana.\nFavorites and type filters; digits hotkey the current selection.\nHOLD the key to aim the cast manually at anyone.");
			Def("Action", "a", Handlers.ActionList, "Special actions list, paid in stamina.\nFavorites and type filters; digits hotkey the current selection.");
			Def("Wear", "w", Handlers.Wear, "Elona-style equipment window: one row per body slot.\nPick a slot to equip or remove; DV/PV sit in the title.");
			Def("Wide", "shift+w", Handlers.WideMenu, "Wide Skill: every spell or action that can target your party.\nEnter casts the pick on ALL of them at once.");
			Def("Tool", "t", Handlers.HoldTool, "Pick a tool from your inventory and hold it in your hands.");
			Def("EmptyHands", "shift+e", Handlers.EmptyHands, "Put away whatever is in your hands.\nCovers a held item, grabbed furniture, or a stuck hotbar selection.");
			Def("Use", "u", Handlers.Use, "Use a usable item from your inventory.");
			Def("Blend", "shift+b", Handlers.Blend, "Dip/blend: pick a potion or liquid, then mix items into it.");
			Def("Throw", "shift+t", Handlers.Throw, "Throw: pick an item, then a target.\nOne flies at your target lock instantly, or the aim cursor opens.\nImportant-starred items and filled containers never fly.");
			Def("Bash", "b", Handlers.Bash, "Bash in a chosen direction.");
			Def("Dig", "m", Handlers.Dig, "Mine. Auto-holds your digging tool.\nOne adjacent minable wall mines instantly, ore veins included;\nseveral walls ask for a direction.\nWalking into a wall with the tool held also mines.\nDo NOT bind this to shift+anything: shift is the dash modifier, and\nauto-repeat mods (AutoAct) read shift as their chain-everything key.");
			Def("Open", "o", Handlers.Open, "Open containers and crafting kits in your inventory, or nearby ones.");
			Def("Close", "shift+c", Handlers.DoorToggle, "Open or close a door in a chosen direction.");
			Def("Pray", "p", Handlers.Pray, "Pray to your god.");
			Def("Offer", "shift+o", Handlers.Offer, "Offer a sacrifice at an adjacent altar.");
			Def("Give", "shift+g", Handlers.Give, "Give to an adjacent character; count spinner for stacks.");
			Def("Interact", "i", Handlers.Interact, "Interaction menu for a direction (talk, give, trade...).");
			Def("Search", "s", Handlers.SearchHere, "Do things on your own tile: search the ground, harvest, collect,\ndig treasure...\nHarvests instantly when something grows where you stand.");
			Def("Chara", "c", Handlers.ShowChara, "Character sheet, classic Elona c.");
			Def("LastMenu", "x", delegate
			{
				(MenuCycle.lastItemMenu ?? (Action)Handlers.ExamineInventory)();
			}, "Reopen the last item menu you used, classic Elona x.\nCovers examine, drop, eat, read, quaff, zap, tool, open, blend and throw.");
			Def("Context", "space", Handlers.ContextAll, "Everything interactable on and around you, Elona Space.\nWhen there is only one thing to do, it fires instantly.");
			Def("Ammo", "shift+a", Handlers.Ammo, "Load a different ammo into your ranged weapon.");
			Def("Party", "shift+q", Handlers.Party, "Party menu: character sheets, browse and take members' items.");
			Def("Target", "keypadMultiply, quote", Handlers.PickTarget, "Classic *: set or change your remembered target.\nSpells and throws then fire at it instantly.\nEnemies and allies lock separately and hold at the same time;\nyourself and your faction count as ally.\nA neutral creature (a little sister for capture balls) locks as a target\nfor throws and heals without exposing her to attack spells.\nSee the NeutralLock config for the other flavors.\nAlso on the apostrophe key, for keyboards without a numpad.");
			Def("SwapTarget", "keypadDivide, semicolon", TargetMode.ToggleSwap, "Aim the NEXT cast at the opposite lock:\na hostile spell at your locked ally, a buff or heal at your locked enemy.\nTap to arm it (tap again cancels), or hold it while casting.");
			Def("Wheel", "z", Handlers.Wheel, "Action wheel: every verb of this mod in one menu, like classic Elona's z.");
			Def("MacroKey1", "none", delegate { Handlers.RunMacro(Macro1); }, "Casts the Macro1 ability sequence.");
			Def("MacroKey2", "none", delegate { Handlers.RunMacro(Macro2); }, "Casts the Macro2 ability sequence.");
			Def("MacroKey3", "none", delegate { Handlers.RunMacro(Macro3); }, "Casts the Macro3 ability sequence.");
			Def("ExamineInventory", "shift+x", Handlers.ExamineInventory, "Open/close your inventory.");
			Def("Hold", "none", Handlers.Hold, "Hold any item in your hands (Elin-specific, unbound by default).");
			Def("Fire", "f", Handlers.FireSmart, "Fire at your target lock, or auto-acquire the nearest enemy.\nWith a ranged weapon equipped it shoots.\nHolding a throwable stack throws ONE per press.\nOtherwise it repeats your last ATTACK spell or action; buffs never ride f.");
			Rebuild();
			Config.SettingChanged += delegate { Rebuild(); };
			try
			{
				new Harmony(Guid).PatchAll();
			}
			catch (Exception ex)
			{
				// a single bad patch target aborts PatchAll and leaves the mod
				// half-applied (menus without weights, dead widgets...): scream
				Log.LogError("ElonaKeys PATCHING FAILED - the mod is broken until this is fixed: " + ex);
			}
			Log.LogInfo("ElonaKeys " + Version + " loaded, " + entries.Count + " binds active.");
		}

		private void Update()
		{
			MenuExtras.OnUpdate();
		}

		private void Def(string name, string def, Action run, string desc)
		{
			defs.Add((Config.Bind("Keys", name, def, desc + "\nFormat: 'e', 'shift+t', 'period', or several separated by commas.\n'none' unbinds the key."), name, run, EAction.None));
		}

		private void DefNative(string name, string def, EAction action, string desc)
		{
			defs.Add((Config.Bind("Keys", name, def, desc + "\nFormat: 'e', 'shift+t'. 'none' unbinds the key."), name, null, action));
		}

		private static void Rebuild()
		{
			entries.Clear();
			foreach ((ConfigEntry<string> cfg, string name, Action run, EAction action) in defs)
			{
				List<Keybind> binds = Keybind.ParseList(cfg.Value);
				if (binds.Count > 0)
				{
					entries.Add(new Entry { name = name, binds = binds, run = run, action = action });
				}
			}
			SuppressBinds = Keybind.ParseList(suppressNativeKeys.Value);
			SuppressAllBinds = Keybind.ParseList(suppressAllKey.Value);
			Quickbind.LoadFrom(quickbinds.Value);
		}

		// first key of a named bind (hold-detection etc.)
		internal static KeyCode GetBindKey(string name)
		{
			foreach ((ConfigEntry<string> cfg, string n, Action run, EAction action) d in defs)
			{
				if (d.n == name)
				{
					List<Keybind> list = Keybind.ParseList(d.cfg.Value);
					return (list.Count > 0) ? list[0].key : KeyCode.None;
				}
			}
			return KeyCode.None;
		}

		// any key of a NAMED bind physically held (swap-while-casting etc.)
		internal static bool BindHeld(string name)
		{
			foreach (Entry e in entries)
			{
				if (e.name != name)
				{
					continue;
				}
				foreach (Keybind b in e.binds)
				{
					if (b.IsHeld())
					{
						return true;
					}
				}
			}
			return false;
		}

		internal static Entry FindPressed()
		{
			foreach (Entry e in entries)
			{
				if (e.IsDown())
				{
					return e;
				}
			}
			return null;
		}

		// any of our verb keys physically held (custom handlers only, not
		// native passthroughs): movement must stay dead the whole press, or
		// holding s a beat too long steps the character south
		internal static bool AnyBindHeld()
		{
			foreach (Entry e in entries)
			{
				if (e.run == null)
				{
					continue;
				}
				foreach (Keybind b in e.binds)
				{
					if (b.IsHeld())
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
