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
		public const string Version = "1.1.0";

		internal static ManualLogSource Log;

		private struct Entry
		{
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
		private static readonly List<(ConfigEntry<string> cfg, Action run, EAction action)> defs =
			new List<(ConfigEntry<string>, Action, EAction)>();
		private static ConfigEntry<bool> overrideGameKeys;

		private void Awake()
		{
			Log = Logger;
			overrideGameKeys = Config.Bind("General", "OverrideGameKeys", true,
				"If true, keys bound below take priority over Elin's own keybinds on conflict (e.g. classic 'q' quaff beats the native empty-hand key). If false, the game's binding wins and the mod key is ignored.");
			// Defaults follow classic Elona (elona.fandom.com/wiki/Controls).
			Def("Get", "g, keypad0", Handlers.Get, "Pick up everything on the tile you are standing on.");
			Def("Drop", "d", Handlers.Drop, "Drop items via menu (stays open to drop several).");
			Def("Eat", "e", Handlers.Eat, "Eat something from your inventory.");
			Def("Quaff", "q", Handlers.Quaff, "Drink a potion, or from an adjacent well/fountain.");
			Def("Read", "r", Handlers.ReadItem, "Read a book, scroll or scrap.");
			Def("Zap", "shift+z", Handlers.Zap, "Zap a rod, then pick a direction (wait key = yourself).");
			Def("Cast", "v", Handlers.Cast, "Open the spell/ability list.");
			Def("Wear", "w", Handlers.Wear, "Equip menu; click to wear/wield, click equipped items to remove.");
			Def("Tool", "t", Handlers.Tool, "Use a usable item from your inventory.");
			Def("Throw", "shift+t", Handlers.Throw, "Throw an item at your target, or pick a direction.");
			Def("Bash", "b", Handlers.Bash, "Bash in a chosen direction.");
			Def("Dig", "shift+d", Handlers.Dig, "Mine the wall in a chosen direction.");
			Def("Open", "o", Handlers.Open, "Open an adjacent container or door (picks locks if locked).");
			Def("Close", "shift+c", Handlers.Close, "Close an adjacent door.");
			Def("Pray", "p", Handlers.Pray, "Pray to your god.");
			Def("Offer", "shift+o", Handlers.Offer, "Offer a sacrifice at an adjacent altar.");
			Def("Give", "shift+g", Handlers.Give, "Give items to an adjacent character.");
			Def("Interact", "i", Handlers.Interact, "Interaction menu for a direction (talk, give, trade...).");
			Def("ExamineInventory", "shift+x", Handlers.ExamineInventory, "Open/close your inventory.");
			Def("Hold", "none", Handlers.Hold, "Hold an item in your hands (Elin-specific, unbound by default).");
			DefNative("Fire", "f", EAction.Fire, "Fire your ranged weapon (native Fire action).");
			Rebuild();
			Config.SettingChanged += delegate { Rebuild(); };
			new Harmony(Guid).PatchAll();
			Log.LogInfo("ElonaKeys " + Version + " loaded, " + entries.Count + " binds active.");
		}

		private void Def(string name, string def, Action run, string desc)
		{
			defs.Add((Config.Bind("Keys", name, def, desc + " Format: 'e', 'shift+t', 'period', comma-separated alternatives, 'none' to unbind."), run, EAction.None));
		}

		private void DefNative(string name, string def, EAction action, string desc)
		{
			defs.Add((Config.Bind("Keys", name, def, desc + " Format: 'e', 'shift+t', 'none' to unbind."), null, action));
		}

		private static void Rebuild()
		{
			entries.Clear();
			foreach ((ConfigEntry<string> cfg, Action run, EAction action) in defs)
			{
				List<Keybind> binds = Keybind.ParseList(cfg.Value);
				if (binds.Count > 0)
				{
					entries.Add(new Entry { binds = binds, run = run, action = action });
				}
			}
		}

		// Called each frame from the AM_Adv._OnUpdateInput prefix, i.e. exactly when
		// the game itself polls gameplay keys (game started, active zone, no modal UI).
		internal static void HandleBinds()
		{
			if (DirPrompt.Active || EInput.isInputFieldActive || EClass.ui.BlockInput)
			{
				return;
			}
			if (EInput.IsConsumed || EInput.isCtrlDown || EInput.isAltDown)
			{
				return;
			}
			EAction current = EInput.action;
			// Without override, the game's own (rebindable) keys win; ShowGrid is the
			// hardcoded 'g' fallback and is always overridable.
			if (!overrideGameKeys.Value && current != EAction.None && current != EAction.ShowGrid)
			{
				return;
			}
			foreach (Entry e in entries)
			{
				if (!e.IsDown())
				{
					continue;
				}
				if (e.action != EAction.None)
				{
					EInput.action = e.action; // native passthrough, game handles it after us
					return;
				}
				EInput.action = EAction.None; // suppress whatever the game mapped to this key
				try
				{
					e.run();
				}
				catch (Exception ex)
				{
					Log?.LogError("ElonaKeys handler failed: " + ex);
				}
				EInput.Consume();
				return;
			}
		}
	}

	[HarmonyPatch(typeof(AM_Adv), "_OnUpdateInput")]
	internal static class Patch_AdvInput
	{
		private static void Prefix()
		{
			Plugin.HandleBinds();
		}
	}

	[HarmonyPatch(typeof(ActionMode), "InputMovement")]
	internal static class Patch_InputMovement
	{
		private static bool Prefix()
		{
			return !DirPrompt.Handle();
		}
	}
}
