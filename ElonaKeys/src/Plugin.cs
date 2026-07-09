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
		public const string Version = "1.26.4";

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
		private static ConfigEntry<int> weightWidth;
		private static ConfigEntry<int> menuWidth;
		private static ConfigEntry<int> nameScrollSlack;
		private static ConfigEntry<string> quickbinds;
		private static ConfigEntry<bool> keyboardContainers;
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
		internal static float WeightWidth => Mathf.Clamp(weightWidth.Value, 30, 160);
		internal static float MenuWidth => Mathf.Clamp(menuWidth.Value, 480, 1100);
		internal static float NameScrollSlack => Mathf.Clamp(nameScrollSlack.Value, 0, 100);

		internal static void SaveQuickbinds(string v)
		{
			quickbinds.Value = v;
		}
		internal static bool KeyboardContainers => keyboardContainers.Value;
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
				"If true, keys bound below take priority over Elin's own keybinds on conflict. If false, the game's binding wins and the mod key is ignored.");
			debugLog = Config.Bind("General", "DebugLog", true,
				"Log why key presses are ignored (for troubleshooting 'keys stopped working'). On by default while the mod stabilizes; set false to quiet the log.");
			menuCategories = Config.Bind("Menus", "CategoryFilter", true,
				"When an item menu would list more than CategoryThreshold items, show a category menu first.");
			categoryThreshold = Config.Bind("Menus", "CategoryThreshold", 25,
				"Item count above which the category menu kicks in.");
			nameDisplay = Config.Bind("Menus", "NameDisplay", "pingpong",
				new ConfigDescription("How long item names are displayed in menus: truncate, pingpong (scroll back and forth), or wrap.",
					new AcceptableValueList<string>("pingpong", "truncate", "wrap")));
			menuPageSize = Config.Bind("Menus", "MenuPageSize", 15,
				"Rows per menu page. Multiple pages are switched with the left/right arrow keys.");
			shiftDirTool = Config.Bind("General", "ShiftDirTool", true,
				"While holding a tool, shift+direction uses it on the adjacent tile (mine, chop, till...) like drag-right-click.");
			bumpToTalk = Config.Bind("General", "BumpToTalk", true,
				"Walking into a friendly NPC talks to them, Elona style (party members still swap places).");
			bumpToUse = Config.Bind("General", "BumpToUse", true,
				"Walking into a solid interactable (quest board, gacha, fountain...) uses it, like Elona's walk-over + Space.");
			isoAim = Config.Bind("General", "IsoAim", true,
				"Direction prompts aim at the tile that LOOKS that way on screen (Elin's grid is rotated 45 degrees). Off = raw grid directions matching movement.");
			suppressNativeKeys = Config.Bind("General", "SuppressNativeKeys", "",
				"Comma-separated keys hidden from the game entirely (use when a native Elin key annoys you and you don't want either binding).");
			suppressAllKey = Config.Bind("General", "SuppressAllKey", "capsLock",
				"PAUSE toggle: while ON, this mod steps aside completely - no mod keys, no keyboard menus, no window redirects, no trade panes. Pure native Elin, the escape hatch when a mod window misbehaves. CapsLock is unused by the game. 'none' disables the toggle.");
			wheelStyle = Config.Bind("Menus", "WheelStyle", "radial",
				new ConfigDescription("z action wheel style: radial (classic Elona circles) or list (window).",
					new AcceptableValueList<string>("radial", "list")));
			wheelInfo = Config.Bind("Menus", "WheelInfo", "Help,Chara,Journal,Log,Equip,Inventory",
				"Verbs on the Info ring (max 6; east/west are ring links). Available: Eat, Quaff, Read, Zap, Cast, Equip, Tool, Use, Blend, Throw, Ammo, Get, Drop, Open, Door, Bash, Dig, Search, Interact, Pray, Offer, Give, Party, Inventory, Help, Chara, Journal, Log, Fire.");
			wheelAction = Config.Bind("Menus", "WheelAction", "Cast,Fire,Get,Search,Ammo,Use",
				"Verbs on the Action ring (max 6, same verb list as WheelInfo).");
			wheelEtc = Config.Bind("Menus", "WheelEtc", "Pray,Interact,Dig,Bash,Offer,Give",
				"Verbs on the Etc ring (max 6, same verb list as WheelInfo).");
			wheelItems = Config.Bind("Menus", "WheelItems", "Eat,Quaff,Read,Zap,Drop,Throw",
				"Verbs on the Items ring (max 6, same verb list as WheelInfo).");
			wheelMisc = Config.Bind("Menus", "WheelMisc", "Tool,Blend,Door,Open,Party",
				"Verbs on the Misc ring (max 6, same verb list as WheelInfo). Leave empty to remove the ring.");
			wheelPosX = Config.Bind("Menus", "WheelPosX", 0f, "Wheel widget offset from screen center (drag the center diamond to move; saved automatically).");
			wheelPosY = Config.Bind("Menus", "WheelPosY", 0f, "Wheel widget offset from screen center (drag the center diamond to move; saved automatically).");
			activateHotkeys = Config.Bind("Hotkeys", "ActivateHotkeys", "shift",
				"shift = shift+1..9 fires ElonaKeys quickbind slots (plain digits stay native quickslots). off = disable. Bind: select an item or spell in any mod menu and press 1-9. Bindings resolve dynamically: bind a rod of identify, ANY rod of identify in your inventory fires.");
			quickbinds = Config.Bind("Hotkeys", "Quickbinds", "",
				"ElonaKeys quickbind registry storage (slot:kind:id:name;...). Edited automatically; clear to reset all slots.");
			abilityColumns = Config.Bind("Menus", "AbilityColumns", "lv,attr,pow,hit,dmg,cost,left",
				"Order of the spell/action list columns, comma-separated. Reorder or REMOVE entries (e.g. 'lv,dmg,cost,left'). Valid: lv, attr, pow, hit, dmg, cost, left.");
			weightWidth = Config.Bind("Menus", "WeightWidth", 48,
				"Width of the right-aligned weight column in pixels (about 4 digits). Longer weights scroll inside it.");
			menuWidth = Config.Bind("Menus", "MenuWidth", 680,
				"Width of the mod's menu windows in pixels.");
			nameScrollSlack = Config.Bind("Menus", "NameScrollSlack", 20,
				"Names overflowing their column by fewer pixels than this render as-is instead of scrolling.");
			shiftBarVisible = Config.Bind("Hotkeys", "ShiftBarVisible", true,
				"Show the OTHER quickslot page (what shift+1..9 fires) as small corner icons on the real hotbar's slots.");
			familyBarVisible = Config.Bind("Menus", "FamilyBarVisible", true,
				"Whether the Elona-style window-family bar (diamond tabs shown while item/chara/spell windows are open; Tab cycles them) starts ENABLED the first time. Also a real Elin widget: manage it in the widget manager.");
			familyBarScale = Config.Bind("Menus", "FamilyBarScale", 0.75f,
				"Size of the family bar (0.4 - 1.5). Also adjustable from the bar's right-click menu.");
			familyBarHint = Config.Bind("Menus", "FamilyBarHint", true,
				"Show the 'Tab [change]' hint line under the family bar.");
			keyboardContainers = Config.Bind("Menus", "KeyboardContainers", true,
				"Opening an unlocked chest/container by ANY route (walking into it, space, mouse) uses the keyboard take/put menu instead of Elin's window. Locked chests and surprise packages stay native.");
			craftingStyle = Config.Bind("Menus", "CraftingStyle", "native",
				new ConfigDescription("native = the game's crafting window, fully keyboard-driven (arrows pick recipes with live preview, +/- count, m = materials, Enter crafts). menu = replace it with the mod's list-style menu.",
					new AcceptableValueList<string>("native", "menu")));
			keyboardCrafters = Config.Bind("Menus", "KeyboardCrafters", true,
				"Item processors (mill, seed maker, grindstone, cooking tools...) open a keyboard ingredient picker on top of the native window; picking ingredients starts the craft. Esc leaves the native window for the mouse.");
			macro1 = Config.Bind("Macros", "Macro1", "", "Comma-separated ability aliases cast in sequence (self/party per spell; targeted spells hit your current enemy). Find aliases via the spell list names or the ability window.");
			macro2 = Config.Bind("Macros", "Macro2", "", "Second macro sequence.");
			macro3 = Config.Bind("Macros", "Macro3", "", "Third macro sequence.");

			// Defaults follow classic Elona (elona.fandom.com/wiki/Controls).
			Def("GetAll", "keypad0", Handlers.GetAll, "Instantly pick up EVERYTHING on your tile, no menu."); // before Get: wins the shared key in old configs
			Def("Get", "g", Handlers.Get, "Pick up: single item instantly, several = selection menu. HOLD to grab anything nearby (including furniture/dropped containers).");
			Def("Drop", "d", Handlers.Drop, "Drop menu with count spinner: Enter on a stack, left/right adjusts, Enter drops. Menu stays open.");
			Def("Eat", "e", Handlers.Eat, "Eat something from your inventory.");
			Def("Quaff", "q", Handlers.Quaff, "Drink a potion, or from an adjacent well/fountain.");
			Def("Read", "r", Handlers.ReadItem, "Read a book, scroll or scrap.");
			Def("Zap", "shift+z", Handlers.Zap, "Zap a rod, then pick a direction (wait key = yourself).");
			Def("Cast", "v", Handlers.Cast, "Spell list (mana): favorites, type filters, digits hotkey the selection.");
			Def("Action", "a", Handlers.ActionList, "Special actions list (stamina): favorites, type filters, digits hotkey the selection.");
			Def("Wear", "w", Handlers.Wear, "Elona-style equipment window: body slot rows, pick a slot to equip or remove; DV/PV in the title.");
			Def("Wide", "shift+w", Handlers.WideMenu, "Wide Skill: list every spell/action that can target your party; Enter casts it on ALL of them.");
			Def("Tool", "t", Handlers.HoldTool, "Pick a tool from your inventory and hold it in your hands.");
			Def("EmptyHands", "shift+e", Handlers.EmptyHands, "Put away whatever is in your hands (held item, grabbed furniture, or a stuck hotbar selection).");
			Def("Use", "u", Handlers.Use, "Use a usable item from your inventory.");
			Def("Blend", "shift+b", Handlers.Blend, "Dip/blend: pick a potion or liquid, then mix items into it.");
			Def("Throw", "shift+t", Handlers.Throw, "Throw: pick an item, then a target from the list or a direction.");
			Def("Bash", "b", Handlers.Bash, "Bash in a chosen direction.");
			Def("Dig", "shift+d", Handlers.Dig, "Mine a wall in a chosen direction (auto-holds your digging tool).");
			Def("Open", "o", Handlers.Open, "Open containers and crafting kits in your inventory, or nearby ones.");
			Def("Close", "shift+c", Handlers.DoorToggle, "Open or close a door in a chosen direction.");
			Def("Pray", "p", Handlers.Pray, "Pray to your god.");
			Def("Offer", "shift+o", Handlers.Offer, "Offer a sacrifice at an adjacent altar.");
			Def("Give", "shift+g", Handlers.Give, "Give to an adjacent character; count spinner for stacks.");
			Def("Interact", "i", Handlers.Interact, "Interaction menu for a direction (talk, give, trade...).");
			Def("Search", "s", Handlers.SearchHere, "Do things on your own tile: search the ground, harvest, collect, dig treasure...");
			Def("Chara", "c", Handlers.ShowChara, "Character sheet, classic Elona c.");
			Def("LastMenu", "x", delegate
			{
				(MenuCycle.lastItemMenu ?? (Action)Handlers.ExamineInventory)();
			}, "Reopen the last-used item menu (examine/drop/eat/read/quaff/zap/tool/open/blend/throw), classic Elona x.");
			Def("Context", "space", Handlers.ContextAll, "Everything interactable on and around you; a single action fires instantly (Elona Space).");
			Def("Ammo", "shift+a", Handlers.Ammo, "Load a different ammo into your ranged weapon.");
			Def("Party", "shift+q", Handlers.Party, "Party menu: character sheets, browse and take members' items.");
			Def("Target", "keypadMultiply, quote", Handlers.PickTarget, "Classic *: set/change the remembered target (spells and throws then fire at it instantly). Also on the apostrophe key for keyboards without a numpad.");
			Def("Wheel", "z", Handlers.Wheel, "Action wheel: every verb of this mod in one menu, like classic Elona's z.");
			Def("MacroKey1", "none", delegate { Handlers.RunMacro(Macro1); }, "Casts the Macro1 ability sequence.");
			Def("MacroKey2", "none", delegate { Handlers.RunMacro(Macro2); }, "Casts the Macro2 ability sequence.");
			Def("MacroKey3", "none", delegate { Handlers.RunMacro(Macro3); }, "Casts the Macro3 ability sequence.");
			Def("ExamineInventory", "shift+x", Handlers.ExamineInventory, "Open/close your inventory.");
			Def("Hold", "none", Handlers.Hold, "Hold any item in your hands (Elin-specific, unbound by default).");
			DefNative("Fire", "f", EAction.Fire, "Fire your ranged weapon (native Fire action).");
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
			defs.Add((Config.Bind("Keys", name, def, desc + " Format: 'e', 'shift+t', 'period', comma-separated alternatives, 'none' to unbind."), name, run, EAction.None));
		}

		private void DefNative(string name, string def, EAction action, string desc)
		{
			defs.Add((Config.Bind("Keys", name, def, desc + " Format: 'e', 'shift+t', 'none' to unbind."), name, null, action));
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
