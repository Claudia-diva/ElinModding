using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ElonaKeys
{
	[BepInPlugin(Guid, Name, Version)]
	public class Plugin : BaseUnityPlugin
	{
		public const string Guid = "elin.elonakeys";
		public const string Name = "Elona Keys";
		public const string Version = "1.9.0";

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
		private static ConfigEntry<string> suppressNativeKeys;
		private static ConfigEntry<string> wheelStyle;
		private static ConfigEntry<string> wheelInfo;
		private static ConfigEntry<string> wheelAction;
		private static ConfigEntry<string> wheelEtc;
		private static ConfigEntry<string> wheelItems;
		private static ConfigEntry<string> wheelMisc;
		private static ConfigEntry<float> wheelPosX;
		private static ConfigEntry<float> wheelPosY;

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
		internal static string WheelStyle => wheelStyle.Value;
		internal static string WheelInfo => wheelInfo.Value;
		internal static string WheelAction => wheelAction.Value;
		internal static string WheelEtc => wheelEtc.Value;
		internal static string WheelItems => wheelItems.Value;
		internal static string WheelMisc => wheelMisc.Value;
		internal static float WheelPosX => wheelPosX.Value;
		internal static float WheelPosY => wheelPosY.Value;

		internal static void SaveWheelPos(float x, float y)
		{
			wheelPosX.Value = x;
			wheelPosY.Value = y;
		}
		internal static List<Keybind> SuppressBinds { get; private set; } = new List<Keybind>();

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
				"How long item names are displayed in menus: truncate, pingpong (scroll back and forth), or wrap.");
			menuPageSize = Config.Bind("Menus", "MenuPageSize", 15,
				"Rows per menu page. Multiple pages are switched with the left/right arrow keys.");
			shiftDirTool = Config.Bind("General", "ShiftDirTool", true,
				"While holding a tool, shift+direction uses it on the adjacent tile (mine, chop, till...) like drag-right-click.");
			bumpToTalk = Config.Bind("General", "BumpToTalk", true,
				"Walking into a friendly NPC talks to them, Elona style (party members still swap places).");
			bumpToUse = Config.Bind("General", "BumpToUse", true,
				"Walking into a solid interactable (quest board, gacha, fountain...) uses it, like Elona's walk-over + Space.");
			suppressNativeKeys = Config.Bind("General", "SuppressNativeKeys", "",
				"Comma-separated keys hidden from the game entirely (use when a native Elin key annoys you and you don't want either binding).");
			wheelStyle = Config.Bind("Menus", "WheelStyle", "radial",
				"z action wheel style: radial (classic Elona circles) or list (window).");
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
				"Digit keys activate Extra-hotbar slots assigned via the menus (hover an item/spell and press 0-9, shift+0-9 for slots 11-20). Activation mode: off, shift (shift+1..0 fires slots 1-10), plain (1..0 fires slots 1-10 and REPLACES the native toolbelt digits; shift+1..0 fires slots 11-20). F1-F8 always work natively for the first slots.");

			// Defaults follow classic Elona (elona.fandom.com/wiki/Controls).
			Def("Get", "g, keypad0", Handlers.Get, "Pick up everything on the tile you are standing on.");
			Def("Drop", "d", Handlers.Drop, "Drop menu with count spinner: Enter on a stack, left/right adjusts, Enter drops. Menu stays open.");
			Def("Eat", "e", Handlers.Eat, "Eat something from your inventory.");
			Def("Quaff", "q", Handlers.Quaff, "Drink a potion, or from an adjacent well/fountain.");
			Def("Read", "r", Handlers.ReadItem, "Read a book, scroll or scrap.");
			Def("Zap", "shift+z", Handlers.Zap, "Zap a rod, then pick a direction (wait key = yourself).");
			Def("Cast", "v", Handlers.Cast, "Open the spell/ability list. Hover a spell and press 0-9 to hotkey it.");
			Def("Wear", "w", Handlers.Wear, "Elona-style equipment window: body slot rows, pick a slot to equip or remove; DV/PV in the title.");
			Def("Tool", "t", Handlers.HoldTool, "Pick a tool from your inventory and hold it in your hands.");
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
			Def("Search", "s", Handlers.SearchHere, "Do things on your own tile: search spots, harvest, dig treasure...");
			Def("Context", "space", Handlers.ContextAll, "Everything interactable on and around you; a single action fires instantly (Elona Space).");
			Def("Ammo", "shift+a", Handlers.Ammo, "Load a different ammo into your ranged weapon.");
			Def("Party", "shift+q", Handlers.Party, "Party menu: character sheets, browse and take members' items.");
			Def("Wheel", "z", Handlers.Wheel, "Action wheel: every verb of this mod in one menu, like classic Elona's z.");
			Def("ExamineInventory", "shift+x", Handlers.ExamineInventory, "Open/close your inventory.");
			Def("Hold", "none", Handlers.Hold, "Hold any item in your hands (Elin-specific, unbound by default).");
			DefNative("Fire", "f", EAction.Fire, "Fire your ranged weapon (native Fire action).");
			Rebuild();
			Config.SettingChanged += delegate { Rebuild(); };
			new Harmony(Guid).PatchAll();
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
	}
}
