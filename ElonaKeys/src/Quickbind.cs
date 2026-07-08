using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// ElonaKeys' OWN hotkey registry, fully separate from the game's quickslot
	// bar (which stays native and untouched). A slot binds a KIND of thing,
	// not one instance: "ability:actFireball" or "item:rod_identify". Firing
	// resolves at press time, so when a bound rod burns out and you pick up
	// another, the new one fires. Assign with a digit inside any of our menus;
	// fire with shift+digit in the world. Persisted in the mod config.
	internal static class Quickbind
	{
		private class Entry
		{
			public bool ability;
			public string key;   // ability alias or thing id
			public string name;  // display name at bind time (messages)
		}

		private static readonly Entry[] slots = new Entry[9];

		public static void LoadFrom(string csv)
		{
			for (int i = 0; i < slots.Length; i++)
			{
				slots[i] = null;
			}
			if (string.IsNullOrWhiteSpace(csv))
			{
				return;
			}
			foreach (string part in csv.Split(';'))
			{
				string[] f = part.Split(':');
				if (f.Length < 3)
				{
					continue;
				}
				int n;
				if (!int.TryParse(f[0], out n) || n < 1 || n > 9)
				{
					continue;
				}
				slots[n - 1] = new Entry
				{
					ability = f[1] == "ability",
					key = f[2],
					name = (f.Length > 3) ? f[3] : f[2]
				};
			}
		}

		private static void Save()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i] != null)
				{
					if (sb.Length > 0)
					{
						sb.Append(';');
					}
					sb.Append(i + 1).Append(':').Append(slots[i].ability ? "ability" : "item").Append(':').Append(slots[i].key).Append(':').Append(slots[i].name?.Replace(";", "").Replace(":", ""));
				}
			}
			Plugin.SaveQuickbinds(sb.ToString());
		}

		public static void AssignAbility(int slot, SourceElement.Row src)
		{
			if (slot < 0 || slot >= 9 || src == null)
			{
				return;
			}
			slots[slot] = new Entry { ability = true, key = src.alias, name = src.GetName() };
			Save();
			SE.Equip();
			Msg.SayRaw("Slot " + (slot + 1) + " = " + src.GetName() + " (shift+" + (slot + 1) + ").");
			ShiftOverlay.SetDirty();
		}

		public static void AssignItem(int slot, Thing t)
		{
			if (slot < 0 || slot >= 9 || t == null)
			{
				return;
			}
			slots[slot] = new Entry { ability = false, key = t.id, name = t.Name };
			Save();
			SE.Equip();
			Msg.SayRaw("Slot " + (slot + 1) + " = " + t.Name + " (shift+" + (slot + 1) + "). Any matching item fires.");
			ShiftOverlay.SetDirty();
		}

		public static void Clear(int slot)
		{
			if (slot >= 0 && slot < 9)
			{
				slots[slot] = null;
				Save();
				ShiftOverlay.SetDirty();
			}
		}

		// small "[3]" tag for menu rows whose item/ability is bound
		public static string TagForItem(string thingId)
		{
			return Tag(ability: false, thingId);
		}

		public static string TagForAbility(string alias)
		{
			return Tag(ability: true, alias);
		}

		private static string Tag(bool ability, string key)
		{
			if (key == null)
			{
				return "";
			}
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i] != null && slots[i].ability == ability && slots[i].key == key)
				{
					return " <color=#9fd6ff>[" + (i + 1) + "]</color>";
				}
			}
			return "";
		}

		// best matching inventory item for an item binding: charged/usable first
		private static Thing ResolveItem(string id)
		{
			Thing best = null;
			foreach (Thing t in EClass.pc.things.List((Thing x) => x.id == id, onlyAccessible: true))
			{
				if (best == null)
				{
					best = t;
					continue;
				}
				bool tCharged = t.c_charges > 0;
				bool bestCharged = best.c_charges > 0;
				if (tCharged && !bestCharged)
				{
					best = t;
				}
			}
			return best;
		}

		public static void Fire(int slot)
		{
			Entry e = (slot >= 0 && slot < 9) ? slots[slot] : null;
			if (e == null)
			{
				SE.BeepSmall();
				Msg.SayRaw("Slot " + (slot + 1) + " is empty (press a digit in a menu to bind).");
				return;
			}
			try
			{
				if (e.ability)
				{
					foreach (Element el in EClass.pc.elements.dict.Values)
					{
						if (el.Value != 0 && el.source.alias == e.key)
						{
							Handlers.CastAbility(el);
							return;
						}
					}
					Msg.SayRaw("You no longer know " + e.name + ".");
					return;
				}
				Thing t = ResolveItem(e.key);
				if (t == null)
				{
					Msg.SayRaw("No " + e.name + " left in your inventory.");
					return;
				}
				ActivateItem(t);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys quickbind fire failed: " + ex);
			}
		}

		// generic activation by trait, mirroring what the dedicated verbs do
		private static void ActivateItem(Thing t)
		{
			if (t.trait is TraitRod)
			{
				Handlers.ZapRod(t);
				return;
			}
			if (t.trait.CanOpenContainer)
			{
				Handlers.ContainerMenu(t); // quickbound chest = quick access
				return;
			}
			if (t.trait.CanUse(EClass.pc))
			{
				t.trait.OnUse(EClass.pc);
				return;
			}
			if (t.trait.CanDrink(EClass.pc))
			{
				t.DoAct(new AI_Drink { target = t });
				return;
			}
			if (t.trait.CanEat(EClass.pc))
			{
				t.DoAct(new AI_Eat { cook = false, target = t });
				return;
			}
			if (t.trait.CanRead(EClass.pc))
			{
				t.DoAct(new AI_Read { target = t });
				return;
			}
			Handlers.ToggleHold(t);
		}

		// draws the slot's icon into the hotbar corner overlay; false = empty
		public static bool DrawIcon(int slot, Image img)
		{
			Entry e = (slot >= 0 && slot < 9) ? slots[slot] : null;
			if (e == null)
			{
				return false;
			}
			try
			{
				if (e.ability)
				{
					SourceElement.Row src = EClass.sources.elements.alias.TryGetValue(e.key);
					if (src == null)
					{
						return false;
					}
					Act a = ACT.Create(src);
					img.material = null;
					a.SetImage(img); // tinted glyph; the overlay backing carries it
					return true;
				}
				Thing t = ResolveItem(e.key);
				if (t == null)
				{
					return false;
				}
				t.SetImage(img);
				Material mat = null;
				try
				{
					mat = EClass.core.refs.matUIObj;
				}
				catch
				{
				}
				if (mat != null)
				{
					img.material = mat;
				}
				else
				{
					img.color = Color.white;
				}
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
