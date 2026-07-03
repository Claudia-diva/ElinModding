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
		public const string Version = "1.0.0";

		internal static ManualLogSource Log;

		private struct Entry
		{
			public Keybind bind;
			public Action run;       // custom handler, consumes input
			public EAction action;   // or native EAction passthrough
		}

		private static readonly List<Entry> entries = new List<Entry>();
		private static readonly List<(ConfigEntry<string> cfg, Action run, EAction action)> defs =
			new List<(ConfigEntry<string>, Action, EAction)>();

		private void Awake()
		{
			Log = Logger;
			Def("Eat", "e", Handlers.Eat, "Select something edible from your inventory and eat it.");
			Def("Quaff", "q", Handlers.Quaff, "Select a potion and drink it.");
			Def("Read", "r", Handlers.ReadItem, "Select a book or scroll and read it.");
			Def("Zap", "z", Handlers.Zap, "Select a rod, then a direction, and zap it.");
			Def("Cast", "shift+z", Handlers.Cast, "Open the ability/spell list.");
			Def("Get", "g", Handlers.Get, "Pick up everything on the tile you are standing on.");
			Def("Drop", "d", Handlers.Drop, "Select an item and drop it at your feet.");
			Def("Wear", "w", Handlers.Wear, "Select equipment and wear/wield it.");
			Def("Tool", "t", Handlers.Hold, "Select an item and hold it in your hands (tools etc.).");
			Def("Use", "u", Handlers.Use, "Select a usable item and use it.");
			Def("Open", "o", Handlers.Open, "Open an adjacent door (picks locks if locked).");
			Def("Close", "c", Handlers.Close, "Close an adjacent door.");
			Def("Bash", "b", Handlers.Bash, "Bash in a chosen direction.");
			Def("Pray", "p", Handlers.Pray, "Pray to your god.");
			Def("Offer", "shift+o", Handlers.Offer, "Offer a sacrifice at an adjacent altar.");
			Def("Throw", "shift+t", Handlers.Throw, "Select an item and throw it at your target or a direction.");
			Def("Interact", "i", Handlers.Interact, "Show the interaction menu for an adjacent tile (talk, give, trade...).");
			Def("ExamineInventory", "x", Handlers.ExamineInventory, "Open/close your inventory.");
			Def("StairsDown", "shift+period", Handlers.StairsDown, "Descend stairs (or enter the zone on the world map).");
			Def("StairsUp", "shift+comma", Handlers.StairsUp, "Ascend stairs.");
			DefNative("Fire", "f", EAction.Fire, "Fire your ranged weapon (native Fire action).");
			Rebuild();
			Config.SettingChanged += delegate { Rebuild(); };
			new Harmony(Guid).PatchAll();
			Log.LogInfo("ElonaKeys " + Version + " loaded, " + entries.Count + " binds active.");
		}

		private void Def(string name, string def, Action run, string desc)
		{
			defs.Add((Config.Bind("Keys", name, def, desc + " Format: 'e', 'shift+t', 'period', 'none' to unbind."), run, EAction.None));
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
				Keybind bind = Keybind.Parse(cfg.Value);
				if (!bind.IsEmpty)
				{
					entries.Add(new Entry { bind = bind, run = run, action = action });
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
			// Respect the game's own (rebindable) keys: if the game already mapped this
			// press to something, let it win. ShowGrid is the hardcoded 'g' fallback and
			// may be overridden by our binds.
			if (current != EAction.None && current != EAction.ShowGrid)
			{
				return;
			}
			foreach (Entry e in entries)
			{
				if (!e.bind.IsDown())
				{
					continue;
				}
				if (e.action != EAction.None)
				{
					EInput.action = e.action; // native passthrough, game handles it after us
					return;
				}
				if (current == EAction.ShowGrid)
				{
					EInput.action = EAction.None;
				}
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
