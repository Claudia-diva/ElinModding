using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Elona window families: Tab cycles the related windows in order
	// (items X,d,e,r,q,Z,t,o,B,T / equipment-chara / spells-actions);
	// x reopens the last-used item menu. Each verb handler registers its
	// position when it opens; MenuSystem/PopupNav call Next() on Tab.
	internal static class MenuCycle
	{
		private static string group;
		private static int index;
		private static int setFrame = -1;
		private static int cycleFrame = -1;

		internal static Action lastItemMenu;

		// true when Next() already ran this frame: MenuSystem and PopupNav both
		// watch Tab, and without this a single press hopped Wear->Chara->Wear
		internal static bool CycledThisFrame => cycleFrame == Time.frameCount;

		internal static string Group => group;
		internal static int Index => index;

		internal static (string name, string key, Action open)[] Entries(string g)
		{
			switch (g)
			{
			case "items":
				return new (string, string, Action)[]
				{
					("Examine", "X", Handlers.ExamineInventory), ("Drop", "d", Handlers.Drop), ("Eat", "e", Handlers.Eat),
					("Read", "r", Handlers.ReadItem), ("Drink", "q", Handlers.Quaff), ("Zap", "Z", Handlers.Zap),
					("Tool", "t", Handlers.HoldTool), ("Open", "o", Handlers.Open), ("Mix", "B", Handlers.Blend), ("Throw", "T", Handlers.Throw)
				};
			case "chara":
				return new (string, string, Action)[] { ("Wear", "w", Handlers.Wear), ("Chara", "c", Handlers.ShowChara) };
			case "ability":
				return new (string, string, Action)[] { ("Spell", "v", Handlers.Cast), ("Skill", "a", Handlers.ActionList) };
			default:
				return null;
			}
		}

		private static (string name, Action open)[] Cycle(string g)
		{
			(string name, string key, Action open)[] e = Entries(g);
			if (e == null)
			{
				return null;
			}
			(string, Action)[] c = new (string, Action)[e.Length];
			for (int i = 0; i < e.Length; i++)
			{
				c[i] = (e[i].name, e[i].open);
			}
			return c;
		}

		public static void Set(string g, int i)
		{
			group = g;
			index = i;
			setFrame = Time.frameCount;
			if (g == "items")
			{
				lastItemMenu = Cycle(g)[i].open;
			}
		}

		// fresh menus outside any family (Give, containers...) drop the cycle so
		// a stale Tab can't jump somewhere surprising. Submenus of an OPEN family
		// window (equip slot pickers, ability filters...) inherit it instead:
		// clearing there was the "Tab dead after browsing equipment slots" bug.
		public static void ClearUnlessJustSet()
		{
			if (setFrame != Time.frameCount && !MenuSystem.Alive)
			{
				group = null;
			}
		}

		public static bool Next()
		{
			(string name, Action open)[] c = (group != null) ? Cycle(group) : null;
			if (c == null)
			{
				if (Plugin.DebugLog)
				{
					Plugin.Log?.LogInfo("ElonaKeys: Tab ignored, no window family registered");
				}
				return false;
			}
			cycleFrame = Time.frameCount;
			MenuSystem.Close();
			// hop until a stop actually OPENS something: an empty stop (Zap
			// with no rods, Read with no books...) used to kill the chain.
			// baseIndex is captured ONCE: each opener re-registers the family
			// index, which made attempt arithmetic double-skip past stops
			int baseIndex = index;
			for (int attempt = 1; attempt <= c.Length; attempt++)
			{
				int ni = (baseIndex + attempt) % c.Length;
				Layer before = (EClass.ui != null) ? EClass.ui.TopLayer : null;
				if (Plugin.DebugLog)
				{
					Plugin.Log?.LogInfo("ElonaKeys: Tab cycle " + group + " -> " + c[ni].name);
				}
				try
				{
					c[ni].open(); // the opener re-registers its own position
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys menu cycle failed: " + ex);
					break;
				}
				if (MenuSystem.Alive || (EClass.ui != null && (object)EClass.ui.TopLayer != (object)before))
				{
					break; // something opened (our menu or a native window)
				}
				if (Plugin.DebugLog)
				{
					Plugin.Log?.LogInfo("ElonaKeys: family stop '" + c[ni].name + "' had nothing to show, skipping");
				}
			}
			return true;
		}
	}

	// Closes the distribution panel host on Escape or a click on the dim
	internal class EKPanelCloser : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
	{
		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				Destroy(gameObject);
			}
		}

		public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
		{
			Destroy(gameObject); // clicked the dim outside the panel
		}
	}

	// Restores a hidden layer's visibility no matter how it dies (pooled
	// layers keep their components; alpha 0 must never survive a despawn)
	internal class EKHideGuard : MonoBehaviour
	{
		private void OnDisable()
		{
			Restore();
		}

		private void OnDestroy()
		{
			Restore();
		}

		private void Restore()
		{
			CanvasGroup cg = GetComponent<CanvasGroup>();
			if (cg != null)
			{
				cg.alpha = 1f;
				cg.interactable = true;
				cg.blocksRaycasts = true;
			}
			Destroy(this);
		}
	}

	// Implementations of each classic Elona action on top of Elin's systems.
	// All menus go through MenuSystem (paginated, keyboard-navigable, weight
	// column, z-sort, shift = back, count spinner on stacks).
	public static class Handlers
	{
		private static Chara pc => EClass.pc;

		// ---------- shared plumbing ----------

		private static string WeightText(int w)
		{
			return ((float)w / 1000f).ToString("0.0") + "s";
		}

		// Full name plus whatever extra detail the game appends (charges, level...)
		private static string DetailName(Thing t)
		{
			string extra;
			try
			{
				extra = t.GetExtraName();
			}
			catch
			{
				extra = "";
			}
			// marked-important items carry the star everywhere (they refuse to
			// be dropped/sold; the mark was invisible in our menus)
			if (t.c_isImportant)
			{
				extra = (extra ?? "") + " <color=#c9a64f>★</color>";
			}
			return string.IsNullOrEmpty(extra) ? t.Name : (t.Name + " " + extra);
		}

		private static void ItemMenu(string title, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg, bool refresh = false, Func<Thing, string> label = null, List<MenuRow> extraRows = null, string category = null, Func<Thing, int> amount = null, Action<Thing, int> onAmount = null)
		{
			List<Thing> things = Matching(filter, category);
			if (things.Count == 0 && (extraRows == null || extraRows.Count == 0))
			{
				Msg.SayRaw(emptyMsg);
				return;
			}
			if (category == null && Plugin.MenuCategories && things.Count > Plugin.CategoryThreshold)
			{
				CategoryMenu(title, things, filter, onSelect, emptyMsg, refresh, label, extraRows, amount, onAmount);
				return;
			}
			List<MenuRow> rows = BuildRows(things, onSelect, label, refresh, extraRows, amount, onAmount);
			if (rows.Count == 0)
			{
				Msg.SayRaw(emptyMsg);
				return;
			}
			string cat = category;
			MenuSystem.Show(title, rows, refresh ? ((Action)delegate
			{
				MenuSystem.SetRows(BuildRows(Matching(filter, cat), onSelect, label, refresh: true, null, amount, onAmount));
			}) : null);
		}

		private static List<Thing> Matching(Func<Thing, bool> filter, string category)
		{
			// TraitAbility things are hotbar spell placeholders, never real items
			List<Thing> things = pc.things.List((Thing t) => !(t.trait is TraitAbility) && Safe(filter, t), onlyAccessible: true);
			if (!string.IsNullOrEmpty(category))
			{
				things.RemoveAll((Thing t) => CategoryName(t) != category);
			}
			return things;
		}

		private static List<MenuRow> BuildRows(List<Thing> things, Action<Thing> onSelect, Func<Thing, string> label, bool refresh, List<MenuRow> extraRows, Func<Thing, int> amount = null, Action<Thing, int> onAmount = null)
		{
			List<MenuRow> rows = new List<MenuRow>();
			if (extraRows != null)
			{
				foreach (MenuRow er in extraRows)
				{
					er.pinTop = true; // utility rows never sort-shuffle into the items
					rows.Add(er);
				}
			}
			foreach (Thing thing in things)
			{
				Thing t = thing;
				bool held = t == pc.held;
				rows.Add(new MenuRow
				{
					// the held item leads the list, marked, in every menu (Elona)
					label = (held ? "(held) " : "") + ((label != null) ? label(t) : DetailName(t)) + Quickbind.TagForItem(t.id),
					icon = t,
					pinTop = held,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					sortCount = t.Num,
					closeOnAct = !refresh,
					maxAmount = (amount != null) ? amount(t) : 0,
					onAmount = (onAmount != null) ? ((Action<int>)delegate(int n) { onAmount(t, n); }) : null,
					act = (onSelect != null) ? ((Action)delegate { onSelect(t); }) : null
				});
			}
			return rows;
		}

		private static string CategoryName(Thing t)
		{
			try
			{
				return t.category.GetName();
			}
			catch
			{
				return "other";
			}
		}

		private static void CategoryMenu(string title, List<Thing> things, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg, bool refresh, Func<Thing, string> label, List<MenuRow> extraRows, Func<Thing, int> amount, Action<Thing, int> onAmount)
		{
			// per category: entry count, stack total and weight, so the z sort
			// modes (weight/count) actually sort the CATEGORIES too
			Dictionary<string, int> counts = new Dictionary<string, int>();
			Dictionary<string, int> nums = new Dictionary<string, int>();
			Dictionary<string, int> weights = new Dictionary<string, int>();
			foreach (Thing t in things)
			{
				string c = CategoryName(t);
				counts.TryGetValue(c, out int n);
				counts[c] = n + 1;
				nums.TryGetValue(c, out int m);
				nums[c] = m + Mathf.Max(1, t.Num);
				weights.TryGetValue(c, out int w);
				weights[c] = w + t.ChildrenAndSelfWeight;
			}
			List<MenuRow> rows = new List<MenuRow>();
			if (extraRows != null)
			{
				foreach (MenuRow er in extraRows)
				{
					er.pinTop = true;
					rows.Add(er);
				}
			}
			rows.Add(new MenuRow
			{
				label = "All items (" + things.Count + ")",
				pinTop = true, // never shuffles into the categories on sort
				closeOnAct = false, // keep this window in the back-stack for shift
				act = delegate { ItemMenu(title, filter, onSelect, emptyMsg, refresh, label, null, "", amount, onAmount); }
			});
			foreach (KeyValuePair<string, int> kv in counts)
			{
				string cat = kv.Key;
				rows.Add(new MenuRow
				{
					label = cat + " (" + kv.Value + ")",
					rightText = WeightText(weights[cat]),
					sortWeight = weights[cat],
					sortCount = nums[cat],
					closeOnAct = false,
					act = delegate { ItemMenu(title + " - " + cat, filter, onSelect, emptyMsg, refresh, label, null, cat, amount, onAmount); }
				});
			}
			MenuSystem.Show(title, rows);
		}

		private static bool Safe(Func<Thing, bool> filter, Thing t)
		{
			try
			{
				return filter(t);
			}
			catch
			{
				return false;
			}
		}

		// Player tile + 8 neighbors.
		private static IEnumerable<Point> Nearby()
		{
			for (int dz = -1; dz <= 1; dz++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					Point p = pc.pos.Copy();
					p.x += dx;
					p.z += dz;
					if (p.IsValid)
					{
						yield return p;
					}
				}
			}
		}

		// see the recipe heal in the held-act merge below
		private static int healedHeldUid = -1;

		internal static ActPlan BuildPlan(Point p, ActInput input = ActInput.AllAction, bool includeHeld = true)
		{
			ActPlan plan = new ActPlan
			{
				input = input
			};
			PointTarget target = new PointTarget
			{
				mouse = false
			};
			target.Update(p);
			plan._Update(target);
			// GENERIC hot-item interactions: the game only adds the hot item's
			// right-click acts in the RightMouse branch. HotItemHeld = held-tool
			// acts (brush, whip, chop...); HotItemNoItem (EMPTY HANDS) = the
			// grab-installed "actHold" (a dropped purse!) and tile harvest.
			// The target-Card branch reads the GLOBAL mouse target: point it
			// at the tile first.
			HotItem hot = EClass.player.currentHotItem;
			if (includeHeld && input == ActInput.AllAction && (hot is HotItemHeld || hot is HotItemNoItem))
			{
				try
				{
					// a held thing whose HotItemHeld never went through
					// OnSetCurrentItem (no WidgetCurrentTool on screen, holds
					// from other code paths) has the static build recipe at
					// null — and with it NO "place" act. Heal once per thing.
					if (hot is HotItemHeld && HotItemHeld.recipe == null && pc.held is Thing heldT && heldT.uid != healedHeldUid)
					{
						healedHeldUid = heldT.uid;
						// OnSetCurrentItem resets disableTool; a grab-as-furniture
						// hold must keep it or potions go back to throw-only
						bool dt = HotItemHeld.disableTool;
						hot.OnSetCurrentItem();
						HotItemHeld.disableTool = dt;
					}
					PointTarget mt = EClass.scene.mouseTarget;
					bool wasMouse = mt.mouse;
					mt.mouse = false;
					mt.Update(p);
					mt.mouse = wasMouse;
					int before = plan.list.Count;
					hot.TrySetAct(plan);
					// drop duplicates the AllAction pass already produced
					for (int i = plan.list.Count - 1; i >= before; i--)
					{
						for (int j = 0; j < before; j++)
						{
							if (plan.list[j].act == plan.list[i].act && plan.list[j].tc == plan.list[i].tc)
							{
								plan.list.RemoveAt(i);
								break;
							}
						}
					}
				}
				catch (Exception ex)
				{
					if (Plugin.DebugLog)
					{
						Plugin.Log?.LogInfo("ElonaKeys held-act merge skipped: " + ex.Message);
					}
				}
			}
			return plan;
		}

		private static List<MenuRow> TileActRows(string actId, string labelPrefix)
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Point p in Nearby())
			{
				foreach (ActPlan.Item i in BuildPlan(p).list)
				{
					if ((i.act as DynamicAct)?.id == actId)
					{
						ActPlan.Item item = i;
						rows.Add(new MenuRow
						{
							label = labelPrefix + (item.tc?.Name ?? ""),
							icon = item.tc as Thing,
							iconCard = (item.tc != null && item.tc.isChara) ? item.tc : null,
							act = delegate
							{
								if (item.Perform())
								{
									EClass.player.EndTurn();
								}
							}
						});
					}
				}
			}
			return rows;
		}

		private static void RunRows(string title, List<MenuRow> rows, string emptyMsg)
		{
			if (rows.Count == 0)
			{
				Msg.SayRaw(emptyMsg);
			}
			else if (rows.Count == 1 && rows[0].act != null)
			{
				rows[0].act();
			}
			else
			{
				MenuSystem.Show(title, rows);
			}
		}

		// ---------- item keys ----------

		public static void Eat()
		{
			MenuCycle.Set("items", 2);
			ItemMenu("Eat what?", (Thing t) => t.trait.CanEat(pc) && !t.isEquipped,
				delegate(Thing t) { t.DoAct(new AI_Eat { cook = false, target = t }); },
				"You have nothing to eat.");
		}

		public static void Quaff()
		{
			MenuCycle.Set("items", 4);
			ItemMenu("Quaff what?", (Thing t) => t.trait.CanDrink(pc),
				delegate(Thing t) { t.DoAct(new AI_Drink { target = t }); },
				"You have nothing to drink.",
				refresh: false, label: null, extraRows: TileActRows("actDrink", "Drink: "));
		}

		public static void ReadItem()
		{
			MenuCycle.Set("items", 3);
			ItemMenu("Read what?", (Thing t) => t.trait.CanRead(pc),
				delegate(Thing t) { t.DoAct(new AI_Read { target = t }); },
				"You have nothing to read.");
		}

		public static void Zap()
		{
			MenuCycle.Set("items", 5);
			ItemMenu("Zap which rod?", (Thing t) => t.trait is TraitRod,
				delegate(Thing t) { ZapRod(t); },
				"You have no rods.");
		}

		internal static void ZapRod(Thing t)
		{
			DirPrompt.Ask("Zap in which direction? (press the wait key to zap yourself)", self: true, delegate(Point p)
			{
				ActZap act = new ActZap { trait = (TraitRod)t.trait };
				if (act.Perform(pc, null, p))
				{
					EClass.player.EndTurn();
				}
			});
		}

		// Elona drop: pick item, spinner picks the count, menu stays open.
		public static void Drop()
		{
			MenuCycle.Set("items", 1);
			ItemMenu("Drop what?", (Thing t) => !t.isEquipped,
				null,
				"You have nothing to drop.",
				refresh: true, label: null, extraRows: null, category: null,
				amount: (Thing t) => t.Num,
				onAmount: delegate(Thing t, int n)
				{
					// Chara.DropThing IGNORES its num parameter (drops the whole
					// stack regardless): split ourselves first
					Thing x = (n >= t.Num) ? t : t.Split(n);
					pc.DropThing(x);
				});
		}

		// Elona equipment window: body slots with what's worn, pick a slot to
		// change it. Footer stats live in the caption.
		public static void Wear()
		{
			MenuCycle.Set("chara", 0);
			MenuSystem.Show(EquipTitle(), BuildEquipRows(), delegate
			{
				MenuSystem.SetRows(BuildEquipRows());
			});
		}

		private static string EquipTitle()
		{
			string title = "Equipment";
			try
			{
				int w = 0;
				foreach (BodySlot s in pc.body.slots)
				{
					if (s.thing != null)
					{
						w += s.thing.ChildrenAndSelfWeight;
					}
				}
				title += "  " + WeightText(w);
				SourceElement.Row dv = EClass.sources.elements.alias.TryGetValue("DV");
				SourceElement.Row pv = EClass.sources.elements.alias.TryGetValue("PV");
				if (dv != null && pv != null)
				{
					title += "  DV/PV: " + pc.Evalue(dv.id) + "/" + pc.Evalue(pv.id);
				}
			}
			catch
			{
			}
			return title;
		}

		private static List<MenuRow> BuildEquipRows()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (BodySlot bodySlot in pc.body.slots)
			{
				BodySlot slot = bodySlot;
				string slotName = SlotName(slot);
				Thing eq = slot.thing;
				if (eq != null && eq.trait is TraitToolBelt)
				{
					continue; // the toolbelt holds the quickslots; keep it out of wear
				}
				rows.Add(new MenuRow
				{
					fixedPrefix = slotName + ":",
					label = (eq != null) ? DetailName(eq) : "-",
					icon = eq,
					rightText = (eq != null) ? WeightText(eq.ChildrenAndSelfWeight) : "-",
					sortWeight = (eq != null) ? eq.ChildrenAndSelfWeight : 0,
					closeOnAct = false,
					act = delegate { SlotMenu(slot, slotName); }
				});
			}
			return rows;
		}

		private static string SlotName(BodySlot slot)
		{
			try
			{
				string name = EClass.sources.elements.map[slot.elementId].GetName();
				if (name == "Light Source")
				{
					name = "Light";
				}
				if (name.Length > 9)
				{
					name = name.Substring(0, 8) + "…";
				}
				return name;
			}
			catch
			{
				return "slot";
			}
		}

		private static void SlotMenu(BodySlot slot, string slotName)
		{
			MenuSystem.Show(slotName, BuildSlotRows(slot), delegate
			{
				MenuSystem.SetRows(BuildSlotRows(slot));
			});
		}

		private static List<MenuRow> BuildSlotRows(BodySlot slot)
		{
			List<MenuRow> rows = new List<MenuRow>();
			if (slot.thing != null && !(slot.thing.trait is TraitToolBelt))
			{
				Thing worn = slot.thing;
				rows.Add(new MenuRow
				{
					label = "(remove)  " + worn.Name,
					icon = worn,
					rightText = WeightText(worn.ChildrenAndSelfWeight),
					closeOnAct = false,
					act = delegate { pc.body.Unequip(worn); }
				});
			}
			foreach (Thing thing in pc.things.List((Thing t) => !(t.trait is TraitAbility) && !t.isEquipped && t.category.slot == slot.elementId, onlyAccessible: true))
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = t.Name,
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					closeOnAct = false,
					act = delegate
					{
						if (!pc.body.Equip(t, slot))
						{
							Msg.SayRaw("You cannot equip that.");
						}
					}
				});
			}
			return rows;
		}

		private static bool IsHeld(Thing t)
		{
			return pc.held != null && t != null && pc.held.uid == t.uid;
		}

		// pinned first row to put the held thing away (identity-proof: uid)
		private static List<MenuRow> HeldRows()
		{
			List<MenuRow> rows = new List<MenuRow>();
			if (pc.held is Thing h)
			{
				rows.Add(new MenuRow
				{
					label = "(held) put away — " + h.Name,
					icon = h,
					act = delegate { ToggleHold(h); }
				});
			}
			return rows;
		}

		public static void HoldTool()
		{
			MenuCycle.Set("items", 6);
			ItemMenu("Hold which tool?", (Thing t) => t.trait is TraitTool && !(t.trait is TraitCatalyst) && !t.isEquipped && !IsHeld(t),
				delegate(Thing t) { ToggleHold(t); },
				"You have no tools.", refresh: false, label: null, extraRows: HeldRows());
		}

		// Hold through the hot-item system so held-tool acts (chop, mine, till,
		// harvest...) appear in action plans; plain pc.HoldCard leaves
		// player.currentHotItem as NoItem and the plan never sees the tool.
		// Picking the currently held thing un-holds it.
		internal static void ToggleHold(Thing t)
		{
			if (IsHeld(t))
			{
				EClass.player.ResetCurrentHotItem();
				if (Plugin.DebugLog)
				{
					Plugin.Log?.LogInfo("ElonaKeys: unheld " + t.Name + " uid=" + t.uid + ", hotItem=" + EClass.player.currentHotItem?.GetType().Name);
				}
				Msg.SayRaw("You put " + t.Name + " away.");
				return;
			}
			EClass.player.SetCurrentHotItem(new HotItemHeld(t));
			if (Plugin.DebugLog)
			{
				Plugin.Log?.LogInfo("ElonaKeys: held " + t.Name + " uid=" + t.uid + ", pc.held=" + (pc.held?.Name ?? "null") + ", hotItem=" + EClass.player.currentHotItem?.GetType().Name);
			}
			// only promise target-use when the item actually has use-on acts
			// (tools, brushes, syringes); scrolls/books/potions use their own
			// verbs (r/q/e work on the held item too, it's pinned on top)
			bool hasUseActs = false;
			try
			{
				hasUseActs = t.trait.CanUse(pc) || t.trait is TraitTool;
			}
			catch
			{
			}
			Msg.SayRaw("Holding " + t.Name + "." + (hasUseActs ? " (i / Space toward a target, s on yourself.)" : ""));
		}

		public static void Use()
		{
			ItemMenu("Use what?", (Thing t) => t.trait.CanUse(pc),
				delegate(Thing t) { t.trait.OnUse(pc); },
				"You have nothing usable.");
		}

		public static void Hold()
		{
			ItemMenu("Hold what in your hands?", (Thing t) => t.trait.CanBeHeld && !t.isEquipped && !IsHeld(t),
				delegate(Thing t) { ToggleHold(t); },
				"You have nothing you can hold.", refresh: false, label: null, extraRows: HeldRows());
		}

		public static void Blend()
		{
			MenuCycle.Set("items", 8);
			ItemMenu("Dip or blend with what?", (Thing t) => t.trait.IsBlendBase,
				delegate(Thing t) { LayerDragGrid.Create(new InvOwnerBlend(t)); },
				"You have nothing to blend with (potions, blessed water and other liquids).");
		}

		public static void Throw()
		{
			MenuCycle.Set("items", 9);
			ItemMenu("Throw what?", (Thing t) => !t.isEquipped && t.trait.CanBeHeld && !(t.trait is TraitToolBelt),
				delegate(Thing t)
				{
					// remembered target throws instantly; otherwise in-world targeting
					Chara quick = TargetMode.QuickTarget(null);
					if (quick != null && pc.pos.Distance(quick.pos) <= 8)
					{
						ActThrow.Throw(pc, quick.pos, t);
						EClass.player.EndTurn();
						return;
					}
					List<Chara> targets = new List<Chara>();
					foreach (Chara c in EClass._map.charas)
					{
						if (c != pc && !c.isDead && pc.CanSee(c) && pc.pos.Distance(c.pos) <= 8)
						{
							targets.Add(c);
						}
					}
					TargetMode.Begin("Throw " + t.Name, targets, delegate(Point p, Chara c)
					{
						ActThrow.Throw(pc, p, t);
						EClass.player.EndTurn();
					}, preferFoe: true);
				},
				"You have nothing to throw.");
		}

		public static void Ammo()
		{
			Thing weapon = pc.ranged;
			if (weapon == null || !(weapon.trait is TraitToolRange range))
			{
				Msg.SayRaw("You are not equipping a ranged weapon.");
				return;
			}
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in pc.things.List((Thing t) => Safe((Thing x) => range.IsAmmo(x), t), onlyAccessible: true))
			{
				Thing t = thing;
				bool loaded = weapon.ammoData == t;
				rows.Add(new MenuRow
				{
					label = t.Name + (loaded ? " <size=12>(loaded)</size>" : ""),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					act = delegate
					{
						Act.CC = pc;
						if (ActRanged.TryReload(weapon, t))
						{
							EClass.player.EndTurn();
						}
					}
				});
			}
			RunRows("Load which ammo into " + weapon.Name + "?", rows, "You have no ammo for " + weapon.Name + ".");
		}

		// ---------- world keys ----------

		private static List<Thing> TilePickables()
		{
			List<Thing> here = new List<Thing>();
			foreach (Thing t in pc.pos.Things)
			{
				if (!t.IsInstalled && t.placeState == PlaceState.roaming && pc.CanPick(t))
				{
					here.Add(t);
				}
			}
			return here;
		}

		// the native theft flow: taking someone's property asks first
		// (Dialog.TryWarnCrime — the same prompt the mouse "Steal" action
		// uses; respects the warnCrime config toggle). Everything our pickup
		// paths grab goes through here, Pick/HoldCard alone silently eats
		// karma + witnesses the crime.
		private static void SafeTake(Thing t, Action take)
		{
			if (t != null && t.isNPCProperty)
			{
				Dialog.TryWarnCrime(delegate { take(); });
			}
			else
			{
				take();
			}
		}

		private static string StealTag(Thing t)
		{
			return (t != null && t.isNPCProperty) ? "  [steal]" : "";
		}

		// the space check every "take into your bag" path shares
		private static bool BackpackFull(Thing t)
		{
			try
			{
				if (pc.things.IsFull(t))
				{
					Msg.SayRaw("Your backpack is full.");
					SE.Beep();
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		// numpad0: hoover the tile. Free things instantly; if any of it is
		// someone's property, ONE crime prompt covers that part of the pile.
		public static void GetAll()
		{
			List<Thing> here = TilePickables();
			if (here.Count == 0)
			{
				Msg.SayRaw("There is nothing here to pick up.");
				return;
			}
			List<Thing> owned = new List<Thing>();
			foreach (Thing t in here)
			{
				if (t == null || t.isDestroyed)
				{
					continue;
				}
				if (t.isNPCProperty)
				{
					owned.Add(t);
				}
				else
				{
					pc.Pick(t);
				}
			}
			if (owned.Count > 0)
			{
				Dialog.TryWarnCrime(delegate
				{
					foreach (Thing t in owned)
					{
						if (t != null && !t.isDestroyed)
						{
							pc.Pick(t);
						}
					}
				});
			}
		}

		// a row can outlive its thing (menus stay open across takes; the
		// crime prompt is async): a taken or destroyed thing must not be
		// acted on twice — that was the hold-g "collect x10" log spam
		private static bool Gone(Thing t)
		{
			try
			{
				return t == null || t.isDestroyed || !t.ExistsOnMap;
			}
			catch
			{
				return true;
			}
		}

		// g: SELECT what to pick up (with counts). HOLD g = grab anything
		// nearby, including installed things like a dropped purse.
		public static void Get()
		{
			MenuExtras.WatchHold(Plugin.GetBindKey("Get"), GrabNearby, requireMenuAlive: false);
			List<Thing> here = TilePickables();
			if (here.Count == 0)
			{
				Msg.SayRaw("There is nothing here to pick up. (Hold g to grab nearby objects.)");
				return;
			}
			if (here.Count == 1)
			{
				Thing single = here[0];
				SafeTake(single, delegate { pc.Pick(single); });
				return;
			}
			MenuSystem.Show("Pick up what?", BuildGetRows(), delegate
			{
				MenuSystem.SetRows(BuildGetRows());
			});
		}

		private static List<MenuRow> BuildGetRows()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in TilePickables())
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = t.Name + StealTag(t),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					sortCount = t.Num,
					closeOnAct = false,
					maxAmount = Mathf.Max(1, t.Num),
					onAmount = delegate(int n)
					{
						if (Gone(t))
						{
							return;
						}
						// split only after the crime prompt says yes
						SafeTake(t, delegate
						{
							if (Gone(t) || BackpackFull(t))
							{
								return;
							}
							Thing taken = (n >= t.Num) ? t : t.Split(n);
							pc.Pick(taken);
						});
					}
				});
			}
			if (rows.Count > 0)
			{
				rows.Add(new MenuRow
				{
					label = "[ Take everything ]",
					pinBottom = true,
					act = delegate { GetAll(); }
				});
			}
			return rows;
		}

		// the FULL native hold, hands version of a grab: SetCurrentHotItem
		// runs HotItemHeld.OnSetCurrentItem, which sets the static build
		// recipe — without it carried furniture has no "place" act anywhere.
		// asFurniture = the vanilla "actHoldTool" flavor: disableTool makes
		// throw-main items (potions!) and tools PLACE instead of throw/use.
		private static void HoldInHands(Thing t, bool asFurniture)
		{
			SafeTake(t, delegate
			{
				if (Gone(t) && t.GetRootCard() != pc)
				{
					return;
				}
				if (!pc.CanLift(t))
				{
					pc.Say("tooHeavy", t);
					return;
				}
				EClass.player.SetCurrentHotItem(new HotItemHeld(t));
				if (asFurniture)
				{
					// AFTER the hold: OnSetCurrentItem resets this flag
					HotItemHeld.disableTool = true;
				}
				if (pc.held == t)
				{
					t.PlaySoundHold(spatial: false);
					pc.renderer.Refresh();
				}
			});
		}

		// the game's own act names, so our rows read exactly like the
		// vanilla context menu (Pick up / Grab / Grab as furniture)
		private static string VerbLang(string idLang, string fallback)
		{
			try
			{
				string s = Lang.Get(idLang);
				return string.IsNullOrEmpty(s) || s == idLang ? fallback : s;
			}
			catch
			{
				return fallback;
			}
		}

		// hold-g: every grabbable in interaction range, loose AND installed,
		// with the vanilla verbs: Pick up = into the backpack, Grab = into
		// your hands, Grab as furniture = hands but Space places it (the
		// actHoldTool flavor for potions, tools and other throw/use items)
		public static void GrabNearby()
		{
			List<MenuRow> rows = BuildGrabRows();
			if (rows.Count == 0)
			{
				Msg.SayRaw("There is nothing nearby to grab.");
				return;
			}
			MenuSystem.Show("Grab what?", rows, delegate
			{
				MenuSystem.SetRows(BuildGrabRows());
			});
		}

		private static List<MenuRow> BuildGrabRows()
		{
			string pickVerb = VerbLang("actPickOne", "Pick up");
			string grabVerb = VerbLang("actHold", "Grab");
			string furnitureVerb = VerbLang("actHoldTool", "Grab as furniture");
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Point point in Nearby())
			{
				foreach (Thing thing in point.Things)
				{
					Thing t = thing;
					bool roaming = !t.IsInstalled && t.placeState == PlaceState.roaming && pc.CanPick(t);
					if (!roaming && !t.IsInstalled)
					{
						continue;
					}
					if (roaming)
					{
						rows.Add(new MenuRow
						{
							label = pickVerb + ": " + t.Name + StealTag(t),
							icon = t,
							sortCount = t.Num,
							closeOnAct = false,
							maxAmount = Mathf.Max(1, t.Num),
							onAmount = delegate(int n)
							{
								if (Gone(t))
								{
									return;
								}
								SafeTake(t, delegate
								{
									if (Gone(t) || BackpackFull(t))
									{
										return;
									}
									Thing taken = (n >= t.Num) ? t : t.Split(n);
									pc.Pick(taken);
								});
							}
						});
					}
					else
					{
						// installed things can go straight to the backpack too:
						// the native uninstall is HoldCard, PickHeld stows it
						rows.Add(new MenuRow
						{
							label = pickVerb + ": " + t.Name + StealTag(t),
							icon = t,
							act = delegate
							{
								SafeTake(t, delegate
								{
									if (Gone(t) || BackpackFull(t))
									{
										return;
									}
									if (!pc.CanLift(t))
									{
										pc.Say("tooHeavy", t);
										return;
									}
									pc.HoldCard(t);
									if (pc.held == t)
									{
										pc.PickHeld();
									}
									EClass.player.RefreshCurrentHotItem();
								});
							}
						});
					}
					// hands flavors, vanilla gates: AllowHold checks CanBeDropped;
					// actHoldTool appears for CanBeHeldAsFurniture (potions, tools)
					if (t.trait.CanBeDropped && !t.isEquipped)
					{
						rows.Add(new MenuRow
						{
							label = grabVerb + ": " + t.Name + StealTag(t),
							icon = t,
							act = delegate { HoldInHands(t, asFurniture: false); }
						});
						if (t.trait.CanBeHeldAsFurniture)
						{
							rows.Add(new MenuRow
							{
								label = furnitureVerb + ": " + t.Name + StealTag(t),
								icon = t,
								act = delegate { HoldInHands(t, asFurniture: true); }
							});
						}
					}
				}
			}
			return rows;
		}

		// shift+e: put away whatever occupies your hands — a held item/tool,
		// grabbed furniture, or a hotbar ability selection that got stuck
		public static void EmptyHands()
		{
			bool had = pc.held != null || (EClass.player.currentHotItem != null && !(EClass.player.currentHotItem is HotItemNoItem));
			// toolbelt tools (shears, sickle, hammer...): the WIDGET keeps a
			// selected slot and re-holds the tool right after a plain reset —
			// Select(-1) is the native deselect that clears both
			bool viaWidget = false;
			try
			{
				if (WidgetCurrentTool.Instance != null)
				{
					WidgetCurrentTool.Instance.Select(-1);
					viaWidget = true;
				}
			}
			catch
			{
			}
			if (!viaWidget)
			{
				EClass.player.ResetCurrentHotItem();
			}
			Msg.SayRaw(had ? "You empty your hands." : "Your hands are already empty.");
			try
			{
				pc.renderer.Refresh();
			}
			catch
			{
			}
		}

		private static void PlanMenu(string title, Point p, string emptyMsg)
		{
			ActPlan plan = BuildPlan(p);
			List<MenuRow> rows = new List<MenuRow>();
			AddPlanRowsOrdered(rows, plan.list, plan.HasMultipleTargets);
			RunRows(title, rows, emptyMsg);
		}

		// space/s row ORDER (user spec): the tile and its items/buildings come
		// FIRST; strangers after; your own party (the mount you sit on, the
		// pet you host) LAST — not the other way around
		private static int PlanBucket(ActPlan.Item item)
		{
			Card tc = item.tc;
			if (tc == null || tc.isThing)
			{
				return 0;
			}
			if (tc.isChara && (tc == pc || tc.Chara.IsPCParty))
			{
				return 2;
			}
			return 1;
		}

		private static void AddPlanRowsOrdered(List<MenuRow> rows, List<ActPlan.Item> items, bool forceName)
		{
			for (int bucket = 0; bucket <= 2; bucket++)
			{
				foreach (ActPlan.Item item in items)
				{
					if (PlanBucket(item) == bucket)
					{
						rows.Add(PlanRow(item, forceName));
					}
				}
			}
		}

		private static MenuRow PlanRow(ActPlan.Item item, bool forceName)
		{
			return new MenuRow
			{
				label = ContextLabel(item, forceName),
				icon = item.tc as Thing,
				iconCard = (item.tc != null && item.tc.isChara) ? item.tc : null,
				// tile acts (Collect, mine...) have no target card: show the
				// map object/block sprite so the row isn't naked
				drawIcon = (item.tc == null) ? TileIconPainter(item.pos) : null,
				act = delegate
				{
					if (item.Perform())
					{
						EClass.player.EndTurn();
					}
				}
			};
		}

		private static Action<Image> TileIconPainter(Point p)
		{
			try
			{
				if (p == null)
				{
					return null;
				}
				RenderRow row = null;
				if (p.cell.obj != 0)
				{
					row = p.cell.sourceObj;
				}
				else if (p.cell.HasBlock)
				{
					row = p.cell.sourceBlock;
				}
				if (row == null)
				{
					return null;
				}
				RenderRow r2 = row;
				return delegate(Image img)
				{
					Sprite sp = r2.GetSprite();
					if (sp == null)
					{
						img.enabled = false;
						return;
					}
					img.material = null;
					img.color = Color.white;
					img.sprite = sp;
					img.SetNativeSize();
				};
			}
			catch
			{
				return null;
			}
		}

		// "Talk - Poppy" instead of six anonymous Talks when several targets exist
		private static string ContextLabel(ActPlan.Item item, bool forceName)
		{
			string label = item.GetTextContext(forceName).Replace("\n", " ");
			if (forceName && item.tc != null && !label.Contains(item.tc.Name))
			{
				label += " <size=13><color=#8f8f7a>- " + item.tc.Name + "</color></size>";
			}
			return label;
		}

		public static void SearchHere()
		{
			Point p = pc.pos.Copy();
			// standing on a grown crop/plant/gatherable: harvest it right away
			TaskHarvest harvest = null;
			try
			{
				harvest = TaskHarvest.TryGetAct(pc, p);
			}
			catch
			{
			}
			if (harvest != null)
			{
				pc.SetAIImmediate(harvest);
				EClass.player.EndTurn();
				return;
			}
			ActPlan plan = BuildPlan(p);
			List<MenuRow> rows = new List<MenuRow>();
			AddPlanRowsOrdered(rows, plan.list, plan.HasMultipleTargets);
			if (rows.Count == 0)
			{
				// nothing else here: classic Elona search-the-ground
				if (!ActWait.SearchMedal(pc, p))
				{
					Msg.SayRaw("You search the ground but find nothing.");
				}
				EClass.player.EndTurn();
				return;
			}
			rows.Add(new MenuRow
			{
				label = "Search the ground",
				act = delegate
				{
					if (!ActWait.SearchMedal(pc, pc.pos))
					{
						Msg.SayRaw("You search the ground but find nothing.");
					}
					EClass.player.EndTurn();
				}
			});
			MenuSystem.Show("Actions here", rows);
		}

		// Elona Space: everything doable on and around you, one action fires instantly.
		public static void ContextAll()
		{
			List<MenuRow> rows = new List<MenuRow>();
			List<ActPlan.Item> gathered = new List<ActPlan.Item>();
			foreach (Point point in Nearby())
			{
				Point p = point;
				// held-item acts (Place, tool use...) only for YOUR OWN tile:
				// merging them for every neighbor flooded the list with eight
				// anonymous "Place" rows aimed who-knows-where. Directional
				// placing lives on i; Space places at your feet.
				ActPlan plan = BuildPlan(p, ActInput.AllAction, includeHeld: p.Equals(pc.pos));
				foreach (ActPlan.Item i in plan.list)
				{
					// pickup belongs to g; the native per-tile pick acts only confuse
					if (i.act is DynamicAct da && (da.id == "actPickAll" || da.id == "actPickOne"))
					{
						continue;
					}
					gathered.Add(i);
				}
			}
			AddPlanRowsOrdered(rows, gathered, forceName: true);
			if (rows.Count == 0)
			{
				// Elona Space: nothing to interact with = WAIT (and since only the
				// first press-frame is claimed, HOLDING space keeps waiting natively)
				InputRouter.QueueNative(EAction.Wait);
				return;
			}
			RunRows("Around you", rows, "There is nothing to interact with here.");
		}

		public static void Open()
		{
			MenuCycle.Set("items", 7);
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in pc.things.List((Thing t) => Safe((Thing x) => x.trait.CanOpenContainer || (x.trait is TraitCrafter && x.trait.CanUse(pc)), t), onlyAccessible: true))
			{
				Thing t = thing;
				bool isContainer = t.trait.CanOpenContainer;
				rows.Add(new MenuRow
				{
					// slots + contents weight per bag, and real sort keys
					label = (isContainer ? "Open: " : "Use: ") + t.Name + (isContainer ? "  <size=13><color=#8f8f7a>[" + t.things.Count + "/" + t.things.MaxCapacity + "]</color></size>" : ""),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					sortCount = t.things.Count,
					closeOnAct = !isContainer, // container menu chains for shift-back
					act = delegate
					{
						if (isContainer)
						{
							ContainerMenu(t);
						}
						else
						{
							t.trait.OnUse(pc);
						}
					}
				});
			}
			foreach (Point p in Nearby())
			{
				foreach (Thing thing in p.Things)
				{
					if (!thing.IsInstalled)
					{
						continue;
					}
					Thing t = thing;
					if (t.trait.CanOpenContainer)
					{
						bool locked = t.c_lockLv > 0;
						rows.Add(new MenuRow
						{
							label = "Open: " + t.Name + " (nearby)" + (locked ? " <color=#c08080>(locked)</color>" : "  <size=13><color=#8f8f7a>[" + t.things.Count + "/" + t.things.MaxCapacity + "]</color></size>"),
							icon = t,
							rightText = WeightText(t.ChildrenAndSelfWeight),
							sortWeight = t.ChildrenAndSelfWeight,
							sortCount = t.things.Count,
							closeOnAct = locked,
							act = delegate
							{
								if (t.c_lockLv > 0)
								{
									pc.SetAIImmediate(new AI_OpenLock { target = t });
								}
								else
								{
									ContainerMenu(t);
								}
							}
						});
					}
					else if (t.trait is TraitCrafter && t.trait.CanUse(pc))
					{
						rows.Add(new MenuRow
						{
							label = "Use: " + t.Name + " (nearby)",
							icon = t,
							act = delegate { t.trait.OnUse(pc); }
						});
					}
				}
			}
			// backpack autopickup settings (player.dataPick) live here: the base
			// bag has no other keyboard home, and single-container auto-open used
			// to make this row unreachable
			rows.Add(new MenuRow
			{
				label = "[ Backpack settings (autopickup)... ]",
				closeOnAct = false,
				pinBottom = true, // sorting must not shuffle the utility row
				act = delegate { BackpackSettings(); }
			});
			if (rows.Count == 1)
			{
				Msg.SayRaw("There is nothing to open here.");
			}
			MenuSystem.Show("Open what?", rows); // never auto-fire a single row
		}

		// autopickup config: which categories get picked up, rotten filter,
		// advanced distribution, name filter — stored on EClass.player.dataPick
		private static void BackpackSettings()
		{
			MenuSystem.Show("Backpack autopickup", BuildAutopickRows(), delegate
			{
				MenuSystem.SetRows(BuildAutopickRows());
			});
		}

		private static List<MenuRow> BuildAutopickRows()
		{
			Window.SaveData sd = EClass.player.dataPick;
			List<MenuRow> rows = new List<MenuRow>();
			void Toggle(string name, bool value, Action flip)
			{
				rows.Add(new MenuRow
				{
					label = name + ":  " + (value ? "ON" : "OFF"), // plain: colored values are unreadable on parchment
					closeOnAct = false,
					act = flip
				});
			}
			Toggle("Refuse rotten items", sd.noRotten, delegate { sd.noRotten = !sd.noRotten; });
			Toggle("Advanced distribution", sd.advDistribution, delegate { sd.advDistribution = !sd.advDistribution; });
			rows.Add(new MenuRow
			{
				label = "[ Picked-up categories... ]",
				closeOnAct = false,
				act = delegate { CategoryFilterMenu(sd, "Autopickup categories"); }
			});
			rows.Add(new MenuRow
			{
				label = "[ Advanced distribution panel... ]",
				act = delegate { OpenAdvDistribution(pc, sd); }
			});
			rows.Add(new MenuRow
			{
				label = sd.filter.IsEmpty() ? "[ Name filter... ]" : "[ Filter: " + sd.filter + " ]",
				closeOnAct = false,
				act = delegate
				{
					Dialog.InputName("distFilter3", sd.filter.IsEmpty(""), delegate(bool cancel, string s)
					{
						if (!cancel)
						{
							sd._filterStrs = null;
							sd.filter = s;
						}
					}, Dialog.InputType.DistributionFilter);
				}
			});
			return rows;
		}

		// keyboard take/put for any container (chests, bags, boxes)
		// Keyboard ingredient picker stacked over the native drag grid of an item
		// processor (mill, seed maker, grindstone...). Picking an ingredient places
		// it in the grid slot the way a mouse transfer would (SetCardGrid +
		// OnProcess); filling the last slot starts the craft (InvOwnerCraft
		// .TryStartCraft). Esc just leaves the native window for the mouse.
		internal static void CrafterIngredientMenu(TraitCrafter crafter, int idx)
		{
			LayerDragGrid lg = LayerDragGrid.Instance;
			if (!(bool)lg || crafter == null || idx >= crafter.numIng)
			{
				return;
			}
			List<MenuRow> rows = new List<MenuRow>();
			List<Thing> cands = pc.things.List((Thing t) => Safe((Thing x) => !(x.trait is TraitAbility) && crafter.IsCraftIngredient(x, idx), t), onlyAccessible: true);
			// home zones: stocked storage counts too, like the native drag grid
			// (InvOwnerCraft.AllowStockIngredients)
			if (EClass._zone.IsPCFaction || EClass._zone is Zone_Tent)
			{
				foreach (Thing t in EClass._map.Stocked.things)
				{
					if (t != null && !t.isDestroyed && !t.isNPCProperty && !cands.Contains(t)
						&& Safe((Thing x) => !(x.trait is TraitAbility) && crafter.IsCraftIngredient(x, idx), t))
					{
						cands.Add(t);
					}
				}
			}
			foreach (Thing thing in cands)
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = t.Name,
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					closeOnAct = false,
					act = delegate
					{
						LayerDragGrid lg2 = LayerDragGrid.Instance;
						if (!(bool)lg2 || lg2.owner == null || idx >= lg2.owner.buttons.Count)
						{
							MenuSystem.Close();
							return;
						}
						lg2.owner.buttons[idx].SetCardGrid(t, lg2.owner.buttons[idx].invOwner);
						lg2.owner.OnProcess(t);
						if (idx + 1 < crafter.numIng)
						{
							CrafterIngredientMenu(crafter, idx + 1);
						}
						else
						{
							MenuSystem.Close();
						}
					}
				});
			}
			if (rows.Count == 0)
			{
				Msg.SayRaw((idx == 0) ? "You carry nothing this station can process." : "You carry no matching second ingredient.");
				return;
			}
			string title = crafter.CrafterTitle.IsEmpty() ? "Process" : crafter.CrafterTitle.lang();
			if (crafter.numIng > 1)
			{
				title += " - ingredient " + (idx + 1) + "/" + crafter.numIng;
			}
			MenuSystem.Show(title, rows, null, keepLayers: true);
		}

		// ---------- draglets: EVERY "pick an item for X" flow ----------
		// One menu for the whole LayerDragGrid family: altar offerings, dip/
		// blend, identify/uncurse/enchant/material scrolls, wizard services,
		// bank deposit, tax box, harvest-quest chest, gacha, recycle, gene
		// machine, mod kit, refuel. Enter mimics the native drop-on-slot:
		// place on the current grid slot, OnProcess, pay the service price.
		internal static void DragletMenu(InvOwnerDraglet owner)
		{
			if (owner == null || LayerDragGrid.Instance == null)
			{
				CloseShopLayers(); // the grid was already hidden: never strand it
				return;
			}
			string title = "";
			try
			{
				title = owner.langWhat.IsEmpty() ? "" : owner.langWhat.lang();
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(title))
			{
				title = "Use which item?";
			}
			MenuSystem.Show(title, BuildDragletRows(owner), delegate
			{
				if (LayerDragGrid.Instance == null)
				{
					MenuSystem.Close(); // the flow finished (consumed its uses)
					return;
				}
				MenuSystem.SetRows(BuildDragletRows(owner));
			}, keepLayers: true);
			MenuSystem.SetOnKill(CloseShopLayers);
		}

		private static List<MenuRow> BuildDragletRows(InvOwnerDraglet owner)
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in pc.things.List((Thing x) => !(x.trait is TraitAbility) && Safe((Thing y) => owner.ShouldShowGuide(y), x), onlyAccessible: true))
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = DetailName(t),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					sortCount = t.Num,
					closeOnAct = false,
					act = delegate { DragletAct(owner, t); }
				});
			}
			if (rows.Count == 0)
			{
				rows.Add(new MenuRow { label = "(nothing usable in your bag)" });
			}
			return rows;
		}

		private static void DragletAct(InvOwnerDraglet owner, Thing t)
		{
			try
			{
				LayerDragGrid grid = LayerDragGrid.Instance;
				if (grid == null || t == null || t.isDestroyed)
				{
					return;
				}
				// paid services (wizard identify...): charge like the native
				// transaction; a free path here would be an exploit
				if (owner.price > 0)
				{
					string cur = owner.currency.ToString().ToLowerInvariant();
					if (pc.GetCurrency(cur) < owner.price)
					{
						Msg.SayRaw("Not enough " + Lang._currency(owner.price, cur) + ".");
						SE.Beep();
						return;
					}
					pc.ModCurrency(-owner.price, cur);
					SE.Play("pay");
				}
				Thing use = owner.SingleTarget ? t.Split(1) : t;
				int idx = Mathf.Clamp(grid.currentIndex, 0, owner.buttons.Count - 1);
				owner.buttons[idx].SetCardGrid(use, owner.buttons[idx].invOwner);
				owner.OnProcess(use);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys draglet action failed: " + ex);
			}
		}

		// ---------- NPC trade (party members / pets): free two-way moves ----------
		private static int tradeView; // 0 = their items, 1 = yours

		public static void TradeMenu(Chara who)
		{
			tradeView = 0;
			TradeMenu(who, replace: false);
		}

		private static void TradeMenu(Chara who, bool replace)
		{
			if (who == null || who.isDead || !who.IsAliveInCurrentZone)
			{
				Msg.SayRaw("They are not here.");
				return;
			}
			bool mine = tradeView == 1;
			string title = (mine ? "Give to " : "Take from ") + who.Name + "  (Tab = " + (mine ? "take" : "give") + ")";
			MenuSystem.NextFooterExtra = who.Name + " " + SlotText(who) + "   you " + SlotText(pc);
			MenuSystem.NextOnTab = delegate
			{
				tradeView = 1 - tradeView;
				TradeMenu(who, replace: true);
			};
			MenuSystem.Show(title, BuildTradeRows(who), delegate
			{
				MenuSystem.SetFooterExtra(who.Name + " " + SlotText(who) + "   you " + SlotText(pc));
				MenuSystem.SetRows(BuildTradeRows(who));
			}, keepLayers: false, replace: replace);
		}

		private static List<MenuRow> BuildTradeRows(Chara who)
		{
			bool mine = tradeView == 1;
			Chara src = mine ? pc : who;
			Chara dst = mine ? who : pc;
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in src.things.List((Thing x) => !(x.trait is TraitAbility) && !x.isEquipped, onlyAccessible: true))
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = DetailName(t),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					sortCount = t.Num,
					closeOnAct = false,
					maxAmount = Mathf.Max(1, t.Num),
					onAmount = delegate(int n)
					{
						if (dst != pc && RefuseFilledContainer(t))
						{
							return; // filled bags only travel into YOUR pack
						}
						if (dst.things.IsFull(t))
						{
							Msg.SayRaw(dst.Name + " cannot carry that.");
							return;
						}
						Thing moved = (n >= t.Num) ? t : t.Split(n);
						dst.Pick(moved);
					}
				});
			}
			if (rows.Count == 0)
			{
				rows.Add(new MenuRow { label = "(nothing to move)" });
			}
			return rows;
		}

		// ---------- the ElonaKeys SHOP window ----------
		// A real mod menu over the native trade: buy view lists the trader's
		// stock with prices, sell view lists your bag with sell prices. Enter
		// routes through the native right-click transaction (money, space,
		// stack dialogs, karma all native). z/x switch buy <-> sell.
		private static int shopView; // 0 = buy, 1 = sell

		public static void ShopMenu()
		{
			shopView = 0; // every shop starts on the buy view
			ShopMenu(replace: false);
		}

		// windows WE manage for a trade/draglet flow: the flow's own windows,
		// NEVER any pre-existing one — and NEVER by moving (positions persist
		// in the save; parking them off-screen wrecked people's layouts).
		// Invisibility only, in place; restored before close (layers are
		// POOLED, a lingering alpha 0 comes back as an invisible window).
		private static readonly List<Layer> hiddenFlowLayers = new List<Layer>();

		internal static void HideTradeLayer(Layer li)
		{
			if (li == null || hiddenFlowLayers.Contains(li))
			{
				return;
			}
			try
			{
				CanvasGroup cg = li.gameObject.GetComponent<CanvasGroup>() ?? li.gameObject.AddComponent<CanvasGroup>();
				cg.alpha = 0f;
				cg.interactable = false;
				cg.blocksRaycasts = false;
				// the guard restores visibility no matter HOW the layer dies:
				// teleports/zone changes close layers without our menu's kill
				// hook, and a pooled layer stuck at alpha 0 infects whatever
				// window reuses it next
				if (li.gameObject.GetComponent<EKHideGuard>() == null)
				{
					li.gameObject.AddComponent<EKHideGuard>();
				}
				hiddenFlowLayers.Add(li);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogWarning("ElonaKeys hide trade layer failed: " + ex.Message);
			}
		}

		internal static void CloseShopLayers()
		{
			try
			{
				foreach (Layer li in hiddenFlowLayers)
				{
					if (li == null || !li)
					{
						continue;
					}
					CanvasGroup cg = li.gameObject.GetComponent<CanvasGroup>();
					if (cg != null)
					{
						cg.alpha = 1f;
						cg.interactable = true;
						cg.blocksRaycasts = true;
					}
					li.Close();
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogWarning("ElonaKeys close trade windows failed: " + ex.Message);
			}
			hiddenFlowLayers.Clear();
		}

		private static void ShopMenu(bool replace)
		{
			InvOwner trader = InvOwner.Trader;
			if (trader == null || ShopTransaction.current == null)
			{
				Msg.SayRaw("No shop is open.");
				CloseShopLayers(); // never leave a hidden pooled layer behind
				return;
			}
			bool sell = shopView == 1;
			string title = (sell ? "Sell to " : "Buy from ") + (trader.owner != null ? trader.owner.Name : "the shop") + "  (Tab = " + (sell ? "buy" : "sell") + ")";
			MenuSystem.NextFooterExtra = ShopFooter(trader);
			MenuSystem.NextRightHeader = "Price";
			// TAB flips buy<->sell (Elona+CGX); z keeps sorting, x examining
			MenuSystem.NextOnTab = delegate
			{
				shopView = 1 - shopView;
				if (InvOwner.Trader != null && ShopTransaction.current != null)
				{
					ShopMenu(replace: true);
				}
			};
			List<MenuRow> shopRows = BuildShopRows();
			if (Plugin.DebugLog)
			{
				Plugin.Log?.LogInfo("ElonaKeys shop menu: " + shopRows.Count + " rows, view=" + (sell ? "sell" : "buy") + ", trader=" + (trader.owner != null ? trader.owner.Name : "?"));
			}
			MenuSystem.Show(title, shopRows, delegate
			{
				// money and buy-back prices move with every transaction
				if (InvOwner.Trader != null)
				{
					MenuSystem.SetFooterExtra(ShopFooter(InvOwner.Trader));
				}
				MenuSystem.SetRows(BuildShopRows());
			}, keepLayers: true, replace: replace);
			// our window IS the shop: when it goes, the whole trade goes
			MenuSystem.SetOnKill(CloseShopLayers);
		}

		// "you have 4,320 orens" in the shop's own currency (or the base's
		// resource for home boards)
		private static string ShopFooter(InvOwner trader)
		{
			try
			{
				if (trader.UseHomeResource)
				{
					return trader.homeResource.Name + ": " + trader.homeResource.value;
				}
				string cur = trader.currency.ToString().ToLowerInvariant();
				return "you have " + Lang._currency(pc.GetCurrency(cur), cur);
			}
			catch
			{
				return "";
			}
		}

		private static List<MenuRow> BuildShopRows()
		{
			InvOwner trader = InvOwner.Trader;
			ShopTransaction session = ShopTransaction.current;
			if (trader == null || session == null)
			{
				return new List<MenuRow> { new MenuRow { label = "(the shop closed)" } };
			}
			bool sell = shopView == 1;
			string cur = trader.currency.ToString().ToLowerInvariant();
			List<MenuRow> rows = new List<MenuRow>();
			List<Thing> pool = new List<Thing>();
			if (sell)
			{
				pool.AddRange(pc.things.List((Thing x) => !(x.trait is TraitAbility) && !x.isEquipped && !x.c_isImportant, onlyAccessible: true));
			}
			else if (trader.Container != null)
			{
				foreach (Thing x in trader.Container.things)
				{
					pool.Add(x);
				}
			}
			foreach (Thing thing in pool)
			{
				Thing t = thing;
				int price = 0;
				try
				{
					// the SESSION price: something bought this visit sells back
					// at what was paid for it, exactly like the native window
					price = session.GetPrice(t, t.Num, sell);
				}
				catch
				{
				}
				if (sell && price <= 0)
				{
					continue; // the trader won't pay for it
				}
				string priceText;
				try
				{
					priceText = Lang._currency(price, cur);
				}
				catch
				{
					priceText = price + " " + cur;
				}
				rows.Add(new MenuRow
				{
					// weight rides along in the name line; the right column is Price
					label = DetailName(t) + " <size=13><color=#8f8f7a>" + WeightText(t.ChildrenAndSelfWeight) + "</color></size>",
					icon = t,
					rightText = priceText,
					rightWidth = 110f,
					sortWeight = t.ChildrenAndSelfWeight,
					sortCount = t.Num,
					closeOnAct = false,
					maxAmount = Mathf.Max(1, t.Num),
					onAmount = delegate(int n) { ShopAct(t, n, sell); }
				});
			}
			if (rows.Count == 0)
			{
				rows.Add(new MenuRow { label = sell ? "(nothing they would pay for)" : "(sold out)" });
			}
			return rows;
		}

		// buy/sell n of a row: a DIRECT transaction, no native windows or grid
		// buttons involved (the old button-click route died for anything in a
		// closed pouch). Mirrors InvOwner.Transaction.Process's trader branch:
		// session-aware price, money, space, identify-on-sell, session register.
		private static void ShopAct(Thing t, int n, bool sell)
		{
			InvOwner trader = InvOwner.Trader;
			ShopTransaction session = ShopTransaction.current;
			if (trader == null || session == null)
			{
				Msg.SayRaw("The shop is closed.");
				return;
			}
			try
			{
				string cur = trader.currency.ToString().ToLowerInvariant();
				bool home = trader.UseHomeResource; // boards paying in a base resource
				n = Mathf.Clamp(n, 1, Mathf.Max(1, t.Num));
				int price = session.GetPrice(t, n, sell);
				if (!sell)
				{
					if (home ? (trader.homeResource.value < price) : (price > pc.GetCurrency(cur)))
					{
						Msg.SayRaw("Not enough " + (home ? trader.homeResource.Name : Lang._currency(price, cur)) + ".");
						SE.Beep();
						return;
					}
					if (pc.things.IsFull(t))
					{
						Msg.SayRaw("Your backpack is full.");
						SE.Beep();
						return;
					}
				}
				else
				{
					if (t.c_isImportant)
					{
						Msg.SayRaw("You should keep that.");
						return;
					}
					if (RefuseFilledContainer(t))
					{
						return; // no selling bags with things still inside
					}
					if (price <= 0 && !session.CanSellBack(t, n))
					{
						Msg.SayRaw("They will not pay for that.");
						return;
					}
				}
				SE.Play(sell ? "sell" : "buy");
				Thing moved = (n >= t.Num) ? t : t.Split(n);
				if (sell)
				{
					if (home)
					{
						trader.homeResource.Mod(price);
					}
					else
					{
						pc.ModCurrency(price, cur);
					}
					// native quirk kept: unidentified goods identify on sale
					// instead of registering for buy-back
					if (!moved.IsIdentified && !session.HasBought(moved))
					{
						moved.Identify(show: true, IDTSource.SuperiorIdentify);
					}
					else
					{
						session.Process(moved, moved.Num, sell: true);
					}
					Msg.Say("sold", moved, Lang._currency(price, cur));
					trader.Container.AddThing(moved);
				}
				else
				{
					if (home)
					{
						trader.homeResource.Mod(-price);
					}
					else
					{
						pc.ModCurrency(-price, cur);
					}
					session.Process(moved, moved.Num, sell: false);
					Msg.Say("bought", moved, Lang._currency(price, cur));
					moved.isNPCProperty = false; // paid for: no theft branch in Pick
					pc.Pick(moved, msg: false);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys shop transaction failed: " + ex);
			}
		}

		internal static void ContainerMenu(Thing container)
		{
			// shipping and delivery chests are FACADES: the native open swaps
			// in a global VIRTUAL container and items go there, never into the
			// chest thing itself (LayerInventory.CreateContainer(Card))
			string title = container.Name;
			try
			{
				if (container.trait is TraitShippingChest)
				{
					Thing v = EClass.game.cards.container_shipping;
					v.things.SetSize(container.things.width, container.things.height);
					EClass.player.uidLastShippedZone = EClass._zone.uid;
					container = v;
				}
				else if (container.trait is TraitDeliveryChest)
				{
					container = EClass.game.cards.container_deliver;
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogWarning("ElonaKeys virtual container mapping failed: " + ex.Message);
			}
			Thing shown = container;
			MenuSystem.NextFooterExtra = SlotText(shown);
			MenuSystem.Show(title, BuildContainerRows(shown), delegate
			{
				MenuSystem.SetRows(BuildContainerRows(shown));
			});
		}

		// the native transfer rule our puts bypassed: a container that still
		// has THINGS INSIDE cannot go into another container (or be sold/
		// given) — only your own backpack may hold filled bags
		private static bool RefuseFilledContainer(Thing t)
		{
			try
			{
				if (t.trait.IsContainer && t.things.Count > 0)
				{
					Msg.SayRaw(t.Name + " must be emptied first.");
					SE.Beep();
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		// shipping/delivery/tax destinations DESTROY or sell their contents:
		// important-marked (starred) items must never slip in
		private static bool RefuseImportantInto(Thing container, Thing t)
		{
			try
			{
				bool special = container.trait.IsSpecialContainer
					|| container == EClass.game.cards.container_shipping
					|| container == EClass.game.cards.container_deliver;
				if (special && t.c_isImportant)
				{
					Msg.SayRaw(t.Name + " is marked as important.");
					SE.Beep();
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		// can this container take t? Special containers (shipping chest,
		// delivery/quest boxes) report IsFull() as ALWAYS true once installed
		// and their destination search inherits that — the native drag works
		// only because slot-targeted drops skip the check. Their real capacity
		// is simply used grid slots vs grid size.
		private static bool ContainerFull(Thing container, Thing t)
		{
			try
			{
				if (container.trait.IsSpecialContainer)
				{
					return container.things.Count >= container.things.MaxCapacity;
				}
				return container.things.IsFull(t);
			}
			catch
			{
				return false; // let AddThing be the judge
			}
		}

		// "[Inv 33/35] [Worn 17/20] [Wgt 45.2/58.0s]": equipment counted apart
		// from carried slots (they share the container and inflated the count),
		// worn shown against the body's slot count, weight against the limit
		private static string SlotText(Card holder)
		{
			try
			{
				int worn = 0;
				int carried = 0;
				foreach (Thing t in holder.things)
				{
					if (t.isEquipped)
					{
						worn++;
					}
					else
					{
						carried++;
					}
				}
				string s = "[Inv " + carried + "/" + holder.things.MaxCapacity + "]";
				Chara ch = holder as Chara;
				if (ch != null)
				{
					int slots = 0;
					try
					{
						slots = ch.body.slots.Count;
					}
					catch
					{
					}
					if (worn > 0 || slots > 0)
					{
						s += " [Worn " + worn + (slots > 0 ? "/" + slots : "") + "]";
					}
					try
					{
						s += " [Wgt " + WeightText(ch.ChildrenWeight).TrimEnd('s') + "/" + WeightText(ch.WeightLimit) + "]";
					}
					catch
					{
					}
				}
				else if (worn > 0)
				{
					s += " [Worn " + worn + "]";
				}
				return s;
			}
			catch
			{
				return "";
			}
		}

		private static List<MenuRow> BuildContainerRows(Thing container)
		{
			List<MenuRow> rows = new List<MenuRow>();
			rows.Add(new MenuRow
			{
				label = "[ Put items in... ]",
				closeOnAct = false,
				pinTop = true,
				act = delegate { PutMenu(container); }
			});
			rows.Add(new MenuRow
			{
				label = "[ Container settings... ]",
				closeOnAct = false,
				pinTop = true,
				act = delegate { ContainerSettings(container); }
			});
			foreach (Thing thing in container.things)
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = DetailName(t),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					closeOnAct = false,
					act = delegate { ContainerItemMenu(container, t); }
				});
			}
			return rows;
		}

		// per-item actions inside a container: take / drop / move elsewhere
		private static void ContainerItemMenu(Thing container, Thing t)
		{
			MenuSystem.Show(t.Name, BuildContainerItemRows(container, t), delegate
			{
				MenuSystem.SetRows(BuildContainerItemRows(container, t));
			});
		}

		private static List<MenuRow> BuildContainerItemRows(Thing container, Thing t)
		{
			List<MenuRow> rows = new List<MenuRow>();
			if (t.isDestroyed || t.parent != container)
			{
				return rows; // moved away; menu closes via refresh
			}
			rows.Add(new MenuRow
			{
				label = "Take",
				icon = t,
				closeOnAct = false,
				maxAmount = t.Num,
				onAmount = delegate(int n)
				{
					if (BackpackFull(t))
					{
						return;
					}
					Thing x = (n >= t.Num) ? t : t.Split(n);
					pc.AddCard(x);
					Msg.SayRaw("You take " + x.Name + ".");
				}
			});
			rows.Add(new MenuRow
			{
				label = "Drop at your feet",
				icon = t,
				closeOnAct = false,
				maxAmount = t.Num,
				onAmount = delegate(int n)
				{
					Thing x = (n >= t.Num) ? t : t.Split(n);
					pc.AddCard(x);
					pc.DropThing(x);
				}
			});
			foreach (Thing other in OtherContainers(container))
			{
				Thing dest = other;
				rows.Add(new MenuRow
				{
					label = "Move to: " + dest.Name,
					icon = dest,
					closeOnAct = false,
					maxAmount = t.Num,
					onAmount = delegate(int n)
					{
						if (RefuseImportantInto(dest, t) || RefuseFilledContainer(t))
						{
							return; // it already said why
						}
						if (ContainerFull(dest, t))
						{
							Msg.SayRaw("The " + dest.Name + " is full.");
							return;
						}
						Thing x = (n >= t.Num) ? t : t.Split(n);
						dest.AddThing(x);
						Msg.SayRaw("You move " + x.Name + " to " + dest.Name + ".");
					}
				});
			}
			return rows;
		}

		private static List<Thing> OtherContainers(Thing except)
		{
			List<Thing> found = new List<Thing>();
			foreach (Thing t in pc.things.List((Thing x) => x != except && x.trait.CanOpenContainer, onlyAccessible: true))
			{
				found.Add(t);
			}
			foreach (Point p in Nearby())
			{
				foreach (Thing t in p.Things)
				{
					if (t != except && t.IsInstalled && t.trait.CanOpenContainer && t.c_lockLv == 0)
					{
						found.Add(t);
					}
				}
			}
			return found;
		}

		// the game's own per-container flags (what the mouse config window edits)
		private static void ContainerSettings(Thing container)
		{
			MenuSystem.Show(container.Name + " settings", BuildSettingsRows(container), delegate
			{
				MenuSystem.SetRows(BuildSettingsRows(container));
			});
		}

		private static List<MenuRow> BuildSettingsRows(Thing container)
		{
			Window.SaveData sd = container.c_windowSaveData;
			if (sd == null)
			{
				sd = new Window.SaveData();
				container.c_windowSaveData = sd;
			}
			List<MenuRow> rows = new List<MenuRow>();
			void Toggle(string name, bool value, Action flip)
			{
				rows.Add(new MenuRow
				{
					// plain text: colored values were unreadable on parchment
					label = name + ":  " + (value ? "ON" : "OFF"),
					closeOnAct = false,
					act = flip
				});
			}
			// --- sort (same fields the native window setting menu writes) ---
			rows.Add(new MenuRow
			{
				label = "Sort mode:  " + sd.sortMode + "  (Enter cycles)",
				closeOnAct = false,
				act = delegate
				{
					UIList.SortMode[] all = (UIList.SortMode[])Enum.GetValues(typeof(UIList.SortMode));
					int cur = Array.IndexOf(all, sd.sortMode);
					sd.sortMode = all[(cur + 1) % all.Length];
				}
			});
			Toggle("Sort ascending", sd.sort_ascending, delegate { sd.sort_ascending = !sd.sort_ascending; });
			Toggle("Always sort", sd.alwaysSort, delegate { sd.alwaysSort = !sd.alwaysSort; });
			// --- distribution ---
			rows.Add(new MenuRow
			{
				// Enter cycles -5..20 with wrap; the raw count spinner showed
				// meaningless internal numbers ("26 (26)")
				label = "Distribution priority:  " + sd.priority + "  (Enter cycles, -5..20)",
				closeOnAct = false,
				act = delegate
				{
					sd.priority = (sd.priority >= 20) ? -5 : (sd.priority + 1);
				}
			});
			Toggle("Only rottable items (cooler/fridge)", sd.onlyRottable, delegate { sd.onlyRottable = !sd.onlyRottable; });
			Toggle("Refuse rotten items", sd.noRotten, delegate { sd.noRotten = !sd.noRotten; });
			Toggle("Advanced distribution", sd.advDistribution, delegate { sd.advDistribution = !sd.advDistribution; });
			rows.Add(new MenuRow
			{
				label = "[ Allowed categories... ]",
				closeOnAct = false,
				act = delegate { CategoryFilterMenu(container); }
			});
			rows.Add(new MenuRow
			{
				// TODO: the slider panel refuses every host we give it (both
				// popup factories self-close, our overlay showed it dim and
				// dead, the vanilla-window bypass toggled the chest shut).
				// Parked per user until the theming/custom-UI update.
				label = "<color=#9a9a9a>[ Advanced distribution - TODO, use the mouse on a vanilla chest window ]</color>",
				closeOnAct = false,
				act = delegate { Msg.SayRaw("Not keyboard-ready yet: open the chest with the mod paused (CapsLock) and use the gear button."); }
			});
			// --- autodump ---
			rows.Add(new MenuRow
			{
				label = "Autodump:  " + sd.autodump + "  (Enter cycles)",
				closeOnAct = false,
				act = delegate
				{
					AutodumpFlag[] order = { AutodumpFlag.existing, AutodumpFlag.sameCategory, AutodumpFlag.distribution, AutodumpFlag.none };
					int cur = Array.IndexOf(order, sd.autodump);
					sd.autodump = order[(cur + 1) % order.Length];
				}
			});
			// --- name filter (native input dialog: keyboard text entry) ---
			rows.Add(new MenuRow
			{
				label = sd.filter.IsEmpty() ? "[ Distribution name filter... ]" : "[ Filter: " + sd.filter + " ]",
				closeOnAct = false,
				act = delegate
				{
					Dialog.InputName("distFilter3", sd.filter.IsEmpty(""), delegate(bool cancel, string s)
					{
						if (!cancel)
						{
							sd._filterStrs = null;
							sd.filter = s;
						}
					}, Dialog.InputType.DistributionFilter);
				}
			});
			return rows;
		}

		// the game's real slider/categorization panel, hosted like the native
		// window-settings menu hosts it (UIInventory.ShowAdvDistribution)
		// the vanilla container window, our redirect bypassed once: its gear
		// button hosts the real distribution panel (mouse territory)
		internal static bool containerRedirectBypass;

		private static void OpenNativeContainer(Card container)
		{
			try
			{
				containerRedirectBypass = true;
				if (container is Thing ct && ct.trait is TraitContainer tc)
				{
					tc.TryOpen();
				}
				else
				{
					LayerInventory.CreateContainer(EClass.pc, container);
				}
				Msg.SayRaw("The panel is behind the window's gear button (mouse).");
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys native container open failed: " + ex);
			}
			finally
			{
				containerRedirectBypass = false;
			}
		}

		private static void OpenAdvDistribution(Card container, Window.SaveData sd)
		{
			try
			{
				// OUR OWN host, no context menus: those self-close the moment
				// the mouse isn't over them (v1) or when input shifts (v2),
				// which for a keyboard user means the panel never survives a
				// frame. A plain dimmed overlay + the panel, Escape or a
				// click outside closes it.
				GameObject host = new GameObject("EKPanelHost", typeof(RectTransform));
				host.transform.SetParent(EClass.ui.transform, worldPositionStays: false);
				RectTransform hrt = (RectTransform)host.transform;
				hrt.anchorMin = Vector2.zero;
				hrt.anchorMax = Vector2.one;
				hrt.offsetMin = Vector2.zero;
				hrt.offsetMax = Vector2.zero;
				Image dim = host.AddComponent<Image>();
				dim.color = new Color(0f, 0f, 0f, 0.35f);
				dim.raycastTarget = true; // clicks outside the panel land here
				UIDistribution ud = Util.Instantiate<UIDistribution>("UI/Layer/UIDistribution", host.transform);
				RectTransform prt = ud.transform as RectTransform;
				prt.anchorMin = new Vector2(0.5f, 0.5f);
				prt.anchorMax = new Vector2(0.5f, 0.5f);
				prt.pivot = new Vector2(0.5f, 0.5f);
				prt.anchoredPosition = Vector2.zero;
				ud.SetContainer(container, sd);
				host.AddComponent<EKPanelCloser>();
				SE.Tab();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys adv distribution panel failed: " + ex);
				Msg.SayRaw("Could not open the distribution panel.");
			}
		}

		// per-category accept/block toggles: a SET ContainerFlag = category BLOCKED
		private static void CategoryFilterMenu(Thing container)
		{
			Window.SaveData sd = container.c_windowSaveData;
			if (sd == null)
			{
				sd = new Window.SaveData();
				container.c_windowSaveData = sd;
			}
			CategoryFilterMenu(sd, "Allowed categories - " + container.Name);
		}

		private static void CategoryFilterMenu(Window.SaveData sd, string title)
		{
			MenuSystem.Show(title, BuildCategoryRows(sd), delegate
			{
				MenuSystem.SetRows(BuildCategoryRows(sd));
			});
		}

		private static List<MenuRow> BuildCategoryRows(Window.SaveData sd)
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (ContainerFlag value in Enum.GetValues(typeof(ContainerFlag)))
			{
				ContainerFlag f = value;
				if (f == ContainerFlag.none)
				{
					continue;
				}
				string name;
				try
				{
					name = EClass.sources.categories.map[f.ToString()].GetName();
				}
				catch
				{
					name = f.ToString();
				}
				bool allowed = !sd.flag.HasFlag(f);
				rows.Add(new MenuRow
				{
					label = (allowed ? "★ " : "☆ ") + name,
					closeOnAct = false,
					act = delegate
					{
						if (sd.flag.HasFlag(f))
						{
							sd.flag &= ~f;
						}
						else
						{
							sd.flag |= f;
						}
					}
				});
			}
			return rows;
		}

		private static void PutMenu(Thing container)
		{
			ItemMenu("Put what into " + container.Name + "?",
				(Thing t) => t != container && !t.isEquipped,
				null,
				"You have nothing to put in.",
				refresh: true, label: null, extraRows: null, category: null,
				amount: (Thing t) => t.Num,
				onAmount: delegate(Thing t, int n)
				{
					if (RefuseImportantInto(container, t) || RefuseFilledContainer(t))
					{
						return; // it already said why
					}
					if (ContainerFull(container, t))
					{
						Msg.SayRaw("The " + container.Name + " is full.");
						return;
					}
					Thing put = (n >= t.Num) ? t : t.Split(n);
					container.AddThing(put);
					Msg.SayRaw("You put " + put.Name + " in.");
				});
		}

		public static void DoorToggle()
		{
			DirPrompt.Ask("Open or close a door in which direction?", self: false, delegate(Point p)
			{
				foreach (Thing t in p.Things)
				{
					if (t.IsInstalled && t.trait is TraitDoor door)
					{
						if (!door.IsOpen())
						{
							if (t.c_lockLv > 0)
							{
								pc.SetAIImmediate(new AI_OpenLock { target = t });
							}
							else
							{
								pc.Say("openDoor", pc, t);
								door.ToggleDoor();
								EClass.player.EndTurn();
							}
						}
						else if (door.CanClose())
						{
							pc.Say("close", pc, t);
							door.ToggleDoor();
							EClass.player.EndTurn();
						}
						else
						{
							Msg.SayRaw("It cannot be closed right now.");
						}
						return;
					}
				}
				Msg.SayRaw("There is no door there.");
			});
		}

		// Elona bash = kick: shove characters, kick doors open, smash blockers.
		public static void Bash()
		{
			DirPrompt.Ask("Bash in which direction?", self: false, delegate(Point p)
			{
				foreach (Chara c in p.Charas)
				{
					if (c != pc)
					{
						if (ACT.Kick.Perform(pc, c, p))
						{
							EClass.player.EndTurn();
						}
						return;
					}
				}
				foreach (Thing t in p.Things)
				{
					if (t.IsInstalled && t.trait is TraitDoor door && !door.IsOpen())
					{
						if (t.c_lockLv > 0)
						{
							pc.PlaySound("kick");
							Msg.SayRaw("The " + t.Name + " is locked.");
						}
						else
						{
							pc.PlaySound("kick");
							pc.Say("bash", pc, t.Name);
							door.ToggleDoor();
							EClass.player.EndTurn();
						}
						return;
					}
				}
				ActBash act = new ActBash();
				if (act.Perform(pc, null, p))
				{
					EClass.player.EndTurn();
				}
				else
				{
					pc.Kick(p, ignoreSelf: true);
					EClass.player.EndTurn();
				}
			});
		}

		public static void Dig()
		{
			Thing tool = pc.held as Thing;
			if (tool == null || !tool.HasElement(220))
			{
				tool = null;
				foreach (Thing t in pc.things.List((Thing x) => x.HasElement(220) && x.trait is TraitTool, onlyAccessible: true))
				{
					tool = t;
					break;
				}
				if (tool == null)
				{
					Msg.SayRaw("You need a digging tool.");
					return;
				}
				EClass.player.SetCurrentHotItem(new HotItemHeld(tool));
			}
			Thing held = tool;
			DirPrompt.Ask("Dig in which direction?", self: false, delegate(Point p)
			{
				if (!p.HasBlock || !TaskMine.CanMine(p, held))
				{
					Msg.SayRaw("You cannot dig there.");
					return;
				}
				pc.SetAIImmediate(new TaskMine { pos = p.Copy() });
			});
		}

		public static void Pray()
		{
			if (ActPray.TryPray(pc))
			{
				EClass.player.EndTurn();
			}
		}

		public static void Offer()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Point p in Nearby())
			{
				foreach (Thing t in p.Things)
				{
					if (t.IsInstalled && t.trait is TraitAltar altar)
					{
						TraitAltar a = altar;
						rows.Add(new MenuRow
						{
							label = "Offer at: " + t.Name,
							icon = t,
							act = delegate { LayerDragGrid.CreateOffering(a); }
						});
					}
				}
			}
			RunRows("Offer where?", rows, "There is no altar nearby.");
		}

		public static void Give()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Point p in Nearby())
			{
				foreach (Chara c in p.Charas)
				{
					if (c != pc)
					{
						Chara target = c;
						bool party = c.IsPCParty;
						rows.Add(new MenuRow
						{
							// party members get the two-way trade (Tab flips
							// give/take); gifting stays one-way for outsiders
							label = (party ? "Trade with: " : "Give to: ") + c.Name,
							iconCard = c,
							closeOnAct = false, // shift returns to this list
							act = delegate
							{
								if (party)
								{
									TradeMenu(target);
								}
								else
								{
									GiveTo(target);
								}
							}
						});
					}
				}
			}
			RunRows("Give to whom?", rows, "There is nobody here to give things to.");
		}

		private static void GiveTo(Chara c)
		{
			ItemMenu("Give what to " + c.Name + "?",
				(Thing t) => !t.trait.CanOnlyCarry && !t.isEquipped && !t.c_isImportant,
				null,
				"You have nothing you can give.",
				refresh: true, label: null, extraRows: null, category: null,
				amount: (Thing t) => t.Num,
				onAmount: delegate(Thing t, int n)
				{
					if (!c.CanAcceptGift(pc, t))
					{
						Msg.SayRaw(c.Name + " cannot take that.");
						return;
					}
					Thing give = (n >= t.Num) ? t : t.Split(n);
					pc.GiveGift(c, give);
				});
		}

		public static void Interact()
		{
			DirPrompt.Ask("Interact with which direction? (press the wait key for yourself)", self: true, delegate(Point p)
			{
				PlanMenu("Interact", p, "There is nothing to interact with.");
			});
		}

		// Party: character sheets and item browsing/taking through our window.
		public static void Party()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Chara chara in pc.party.members)
			{
				if (chara == pc)
				{
					continue;
				}
				Chara c = chara;
				rows.Add(new MenuRow
				{
					label = c.Name + " - Character sheet",
					iconCard = c,
					act = delegate { EClass.ui.AddLayer<LayerChara>().SetChara(c); }
				});
				rows.Add(new MenuRow
				{
					label = c.Name + " - Items",
					iconCard = c,
					closeOnAct = false,
					// the SAME two-way trade the Give menu opens: their items /
					// yours, Tab flips sides (the old browse-only view had no
					// give direction and no working Tab)
					act = delegate { TradeMenu(c); }
				});
			}
			RunRows("Party", rows, "You have no party members with you.");
		}

		private static void PartyInventory(Chara c)
		{
			PartyInventory(c, replace: false);
		}

		private static void PartyInventory(Chara c, bool replace)
		{
			// Tab (or period/comma) hops to the next party member's bag
			List<Chara> members = new List<Chara>();
			foreach (Chara m in pc.party.members)
			{
				if (m != pc && !m.isDead)
				{
					members.Add(m);
				}
			}
			int idx = Mathf.Max(0, members.IndexOf(c));
			int count = Mathf.Max(1, members.Count);
			MenuSystem.NextFooterExtra = c.Name + " " + SlotText(c);
			if (members.Count > 1)
			{
				MenuSystem.NextOnTab = delegate
				{
					PartyInventory(members[(idx + 1) % count], replace: true);
				};
				MenuSystem.NextOnSwap = delegate(int dir)
				{
					PartyInventory(members[((idx + dir) % count + count) % count], replace: true);
				};
			}
			MenuSystem.Show(c.Name + "'s items" + (members.Count > 1 ? "  (Tab = next member)" : ""), BuildPartyRows(c), delegate
			{
				MenuSystem.SetFooterExtra(c.Name + " " + SlotText(c));
				MenuSystem.SetRows(BuildPartyRows(c));
			}, keepLayers: false, replace: replace);
		}

		private static List<MenuRow> BuildPartyRows(Chara c)
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in c.things.List((Thing t) => !(t.trait is TraitAbility), onlyAccessible: true))
			{
				Thing t = thing;
				bool worn = t.isEquipped;
				rows.Add(new MenuRow
				{
					label = t.Name + (worn ? " <size=12>(equipped)</size>" : ""),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					closeOnAct = false,
					maxAmount = worn ? 0 : t.Num,
					onAmount = worn ? null : ((Action<int>)delegate(int n)
					{
						if (BackpackFull(t))
						{
							return;
						}
						Thing take = (n >= t.Num) ? t : t.Split(n);
						pc.AddCard(take);
						Msg.SayRaw("You take " + take.Name + " from " + c.Name + ".");
					}),
					act = worn ? ((Action)delegate { Msg.SayRaw("Unequip it through " + c.Name + "'s sheet first."); }) : null
				});
			}
			return rows;
		}

		// Classic 'z' action wheel: Elona's linked rings (Info/Action/Etc/Items),
		// left/right switches ring, last ring remembered. Window as fallback.
		public static void Wheel()
		{
			if (Plugin.WheelStyle != "list")
			{
				RadialWheel.Toggle(BuildRings());
				return;
			}
			WheelWindow();
		}

		private static List<RadialWheel.Ring> BuildRings()
		{
			List<RadialWheel.Ring> rings = new List<RadialWheel.Ring>();
			void AddRing(string name, string csv)
			{
				RadialWheel.Ring ring = new RadialWheel.Ring { name = name };
				foreach (string raw in csv.Split(','))
				{
					string verb = raw.Trim();
					if (verb.Length > 0 && wheelVerbs.TryGetValue(verb, out (string label, Action act) v))
					{
						ring.items.Add((v.label, v.act));
					}
				}
				if (ring.items.Count > 0)
				{
					rings.Add(ring);
				}
			}
			AddRing("Info", Plugin.WheelInfo);
			AddRing("Action", Plugin.WheelAction);
			AddRing("Etc", Plugin.WheelEtc);
			AddRing("Items", Plugin.WheelItems);
			AddRing("Misc", Plugin.WheelMisc);
			if (rings.Count == 0)
			{
				RadialWheel.Ring all = new RadialWheel.Ring { name = "Actions" };
				foreach (KeyValuePair<string, (string label, Action act)> kv in wheelVerbs)
				{
					all.items.Add((kv.Value.label, kv.Value.act));
				}
				rings.Add(all);
			}
			return rings;
		}

		private static readonly Dictionary<string, (string label, Action act)> wheelVerbs =
			new Dictionary<string, (string, Action)>(StringComparer.OrdinalIgnoreCase)
		{
			{ "Eat", ("Eat", Eat) },
			{ "Quaff", ("Quaff", Quaff) },
			{ "Read", ("Read", ReadItem) },
			{ "Zap", ("Zap", Zap) },
			{ "Cast", ("Cast", Cast) },
			{ "Action", ("Action", ActionList) },
			{ "Equip", ("Equip", Wear) },
			{ "Tool", ("Tool", HoldTool) },
			{ "Use", ("Use", Use) },
			{ "Blend", ("Blend", Blend) },
			{ "Throw", ("Throw", Throw) },
			{ "Ammo", ("Ammo", Ammo) },
			{ "Get", ("Get", Get) },
			{ "Drop", ("Drop", Drop) },
			{ "Open", ("Open", Open) },
			{ "Door", ("Door", DoorToggle) },
			{ "Bash", ("Bash", Bash) },
			{ "Dig", ("Dig", Dig) },
			{ "Search", ("Search", SearchHere) },
			{ "Interact", ("Interact", Interact) },
			{ "Pray", ("Pray", Pray) },
			{ "Offer", ("Offer", Offer) },
			{ "Give", ("Give", Give) },
			{ "Party", ("Party", Party) },
			{ "Inventory", ("Inventory", ExamineInventory) },
			{ "Help", ("Help", ShowHelp) },
			{ "Chara", ("Chara", ShowChara) },
			{ "Journal", ("Journal", ShowJournal) },
			{ "Log", ("Log", ShowLog) },
			{ "Fire", ("Fire", FireRanged) }
		};

		public static void ShowHelp()
		{
			LayerHelp.Toggle("general", "1");
		}

		public static void ShowChara()
		{
			MenuCycle.Set("chara", 1);
			EClass.ui.ToggleLayer<LayerChara>()?.SetChara(pc);
		}

		public static void ShowJournal()
		{
			EClass.ui.ToggleLayer<LayerJournal>();
		}

		public static void ShowLog()
		{
			WidgetMainText.ToggleLog();
		}

		public static void FireRanged()
		{
			InputRouter.QueueNative(EAction.Fire);
		}

		private static void WheelWindow()
		{
			List<MenuRow> rows = new List<MenuRow>();
			void Sub(string label, Action act) // opens one of our menus; shift returns here
			{
				rows.Add(new MenuRow { label = label, closeOnAct = false, act = act });
			}
			void Direct(string label, Action act) // acts or opens a game window
			{
				rows.Add(new MenuRow { label = label, act = act });
			}
			Sub("Eat", Eat);
			Sub("Quaff", Quaff);
			Sub("Read", ReadItem);
			Sub("Zap a rod", Zap);
			Direct("Cast a spell", Cast);
			Sub("Equipment", Wear);
			Sub("Hold a tool", HoldTool);
			Sub("Use an item", Use);
			Sub("Dip/blend", Blend);
			Sub("Throw", Throw);
			Sub("Load ammo", Ammo);
			Direct("Get items here", Get);
			Sub("Drop", Drop);
			Sub("Open a container", Open);
			Direct("Open/close a door", DoorToggle);
			Direct("Bash", Bash);
			Direct("Dig a wall", Dig);
			Sub("Actions on this tile", SearchHere);
			Direct("Interact...", Interact);
			Direct("Pray", Pray);
			Sub("Offer", Offer);
			Sub("Give", Give);
			Sub("Party", Party);
			Direct("Inventory", ExamineInventory);
			MenuSystem.Show("Action wheel", rows);
		}

		// ---------- spells and abilities ----------

		// v = spells (mana, group SPELL), a = actions (stamina, group ABILITY).
		// Both menus: favorites, type filters (attack/buff/hex/heal), favorite
		// management, native targeting via ButtonAbility.TryUse, digit hotkeys.
		// tap = quick flow (remembered target casts instantly); HOLD the key
		// ~0.5s after the menu opens = manual targeting for every cast
		internal static bool ManualCastMode;

		public static void Cast()
		{
			MenuCycle.Set("ability", 0);
			ManualCastMode = false;
			AbilityMenu(spells: true, bucket: null, favOnly: false);
			MenuExtras.WatchCastHold(Plugin.GetBindKey("Cast"));
		}

		public static void ActionList()
		{
			MenuCycle.Set("ability", 1);
			ManualCastMode = false;
			AbilityMenu(spells: false, bucket: null, favOnly: false);
			MenuExtras.WatchCastHold(Plugin.GetBindKey("Action"));
		}

		private static string TypeBucket(Element e)
		{
			string t = (e.source.abilityType != null && e.source.abilityType.Length > 0) ? e.source.abilityType[0] : "";
			switch (t)
			{
			case "attack":
			case "dot":
				return "attack";
			case "buff":
			case "buffStats":
				return "buff";
			case "debuff":
			case "debuffStats":
				return "hex";
			case "heal":
				return "heal";
			default:
				return "other";
			}
		}

		private static List<Element> KnownAbilities(bool spells)
		{
			List<Element> known = new List<Element>();
			foreach (Element e in pc.elements.dict.Values)
			{
				if (e.Value != 0 && e.source.group == (spells ? "SPELL" : "ABILITY"))
				{
					known.Add(e);
				}
			}
			known.Sort((Element a, Element b) => string.Compare(a.source.GetName(), b.source.GetName(), StringComparison.OrdinalIgnoreCase));
			return known;
		}

		private static void AbilityMenu(bool spells, string bucket, bool favOnly)
		{
			string what = spells ? "Cast" : "Action";
			List<Element> known = KnownAbilities(spells);
			List<MenuRow> rows = new List<MenuRow>();
			// favorites listed first, no extra press; left/right switch pages
			for (int pass = 0; pass < 2; pass++)
			{
				foreach (Element element in known)
				{
					Element e = element;
					bool fav = EClass.player.favAbility.Contains(e.id);
					if (fav != (pass == 0))
					{
						continue;
					}
					if (favOnly && !fav)
					{
						continue;
					}
					if (bucket != null && TypeBucket(e) != bucket)
					{
						continue;
					}
					rows.Add(AbilityRow(e, spells));
				}
			}
			if (bucket == null && !favOnly)
			{
				// pinBottom: z-sorting must never shuffle these into the spells
				rows.Add(new MenuRow
				{
					label = "[ Filter by type... ]",
					closeOnAct = false,
					pinBottom = true,
					act = delegate { FilterMenu(spells); }
				});
				rows.Add(new MenuRow
				{
					label = "[ Manage favorites... ]",
					closeOnAct = false,
					pinBottom = true,
					act = delegate { FavoriteMenu(spells); }
				});
				rows.Add(new MenuRow
				{
					label = "(open the ability window...)",
					pinBottom = true,
					act = delegate { EClass.ui.ToggleAbility(); }
				});
			}
			string title = what + ((bucket != null) ? (" - " + bucket) : (favOnly ? " - favorites" : "")) + " what?";
			if (rows.Count == 0)
			{
				Msg.SayRaw(spells ? "You know no spells like that." : "You have no such actions.");
			}
			else
			{
				MenuSystem.Show(title, rows);
			}
		}

		private static int BucketIndex(Element e)
		{
			switch (TypeBucket(e))
			{
			case "attack": return 4;
			case "buff": return 3;
			case "heal": return 2;
			case "hex": return 1;
			default: return 0;
			}
		}

		private static MenuRow AbilityRow(Element e, bool spells)
		{
			bool fav = EClass.player.favAbility.Contains(e.id);
			int stock = 0;
			try
			{
				stock = e.PotentialAsStock ? e.vPotential : e.Potential;
			}
			catch
			{
			}
			return new MenuRow
			{
				// the attribute moved into its own column; the label keeps only
				// the category bucket
				label = (fav ? "★ " : "") + e.source.GetName() + Quickbind.TagForAbility(e.source.alias) + " <size=13><color=#8f8f7a>" + TypeBucket(e) + "</color></size>",
				cols = BuildAbilityCols(e, spells),  // aligned Lv/Pow/Hit/Dmg/Cost/Left
				sortWeight = BucketIndex(e) * 1000, // z-sort mode 2 = by category
				sortCount = stock,                  // z-sort mode 3 = by stock
				actSrc = e.source,
				ability = e,
				act = delegate { CastAbility(e); }
			};
		}

		// aligned column values [Lv, Attr, Pow, Hit, Dmg, Cost, Left]; the
		// original cost lives in the tooltip. Pow scales with level and gear.
		private static string[] BuildAbilityCols(Element e, bool spells)
		{
			string lv = "";
			string attr = "";
			string pow = "";
			string hit = "";
			string dmg = "";
			string cost = "";
			string left = "";
			try
			{
				lv = e.DisplayValue.ToString();
			}
			catch
			{
			}
			try
			{
				if (!e.source.aliasParent.IsEmpty())
				{
					attr = e.source.aliasParent.ToUpperInvariant();
				}
			}
			catch
			{
			}
			try
			{
				int p = e.GetPower(pc);
				if (p > 0)
				{
					pow = p.ToString();
				}
			}
			catch
			{
			}
			try
			{
				if (e is Spell)
				{
					hit = pc.CalcCastingChance(e) + "%";
				}
			}
			catch
			{
			}
			try
			{
				Dice d = Dice.Create(e, pc);
				if (d != null)
				{
					dmg = d.ToString(); // plain: the tinted version was unreadable
				}
			}
			catch
			{
			}
			try
			{
				Act a = ACT.Create(e.source);
				int cur = a.GetCost(pc).cost;
				if (cur > 0)
				{
					cost = cur + (spells ? "mp" : "sp");
				}
			}
			catch
			{
			}
			try
			{
				left = e.PotentialAsStock
					? ("<color=#3a6ea5>x" + e.vPotential + "</color>")
					: ("<color=#8f8f7a>" + e.Potential + "%</color>");
			}
			catch
			{
			}
			return new string[] { lv, attr, pow, hit, dmg, cost, left };
		}

		// x on an ability row: aim THIS cast manually with the free cursor.
		// Self-only and party-wide casts have nothing to aim: say so and keep
		// the menu open instead of closing into a pointless cursor.
		internal static void ManualTargetCast(Element e)
		{
			try
			{
				TargetType tt = ACT.Create(e.source).TargetType;
				if (tt.Range == TargetRange.Self || tt.Range == TargetRange.Party || tt.ForceParty)
				{
					Msg.SayRaw(e.source.GetName() + " has no target to aim (Enter casts it).");
					return;
				}
			}
			catch
			{
			}
			MenuSystem.Close();
			ManualCastMode = true;
			CastAbility(e);
		}

		// w in the ability menus: cast the selected spell on the WHOLE PARTY
		// (Elona's Wide Skill), via the game's native multicast
		internal static void WideCast(Element e)
		{
			try
			{
				Act act = (e as Act) ?? ACT.Create(e.source);
				if (!CanWide(act))
				{
					Msg.SayRaw(e.source.GetName() + " cannot multicast on your party.");
					return;
				}
				// the game SILENTLY downgrades a pt cast to a self-cast when the
				// spell stock is below the party count — say it instead
				int party = PartySize();
				if (e is Spell && e.vPotential < party)
				{
					Msg.SayRaw("Not enough spell stock for the whole party (need " + party + ", have " + e.vPotential + ").");
					return;
				}
				MenuSystem.Close();
				if (pc.UseAbility(act, null, null, pt: true))
				{
					EClass.player.EndTurn();
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys wide cast failed: " + ex);
			}
		}

		// Wide key (shift+w): ONLY what can genuinely multicast on your party
		// (heals, buffs). Hostile-party spells are excluded: the game's pt
		// cast loops ENEMIES for those ("Nothing seems to happen" in town).
		private static bool CanWide(Act act)
		{
			try
			{
				if (act.IsTargetHostileParty())
				{
					return false;
				}
				TargetType tt = act.TargetType;
				return tt.CanSelectParty || tt.ForceParty || tt.Range == TargetRange.Party;
			}
			catch
			{
				return false;
			}
		}

		private static int PartySize()
		{
			int n = 0;
			try
			{
				foreach (Chara m in EClass.pc.party.members)
				{
					if (m != null && !m.isDead && m.IsAliveInCurrentZone)
					{
						n++;
					}
				}
			}
			catch
			{
			}
			return Mathf.Max(1, n);
		}

		public static void WideMenu()
		{
			List<MenuRow> rows = new List<MenuRow>();
			for (int pass = 0; pass < 2; pass++)
			{
				bool spells = pass == 0;
				foreach (Element element in KnownAbilities(spells))
				{
					Element e = element;
					if (!CanWide((e as Act) ?? ACT.Create(e.source)))
					{
						continue;
					}
					MenuRow row = AbilityRow(e, spells);
					row.act = delegate { WideCast(e); };
					rows.Add(row);
				}
			}
			RunRows("Wide cast on the party - what?", rows, "Nothing you know can multicast on your party.");
		}

		private static void FilterMenu(bool spells)
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (string b in new string[] { "attack", "buff", "hex", "heal", "other" })
			{
				string bucket = b;
				rows.Add(new MenuRow
				{
					label = bucket,
					closeOnAct = false,
					act = delegate { AbilityMenu(spells, bucket, favOnly: false); }
				});
			}
			MenuSystem.Show("Filter by type", rows);
		}

		// Enter toggles the star instead of casting
		private static void FavoriteMenu(bool spells)
		{
			MenuSystem.Show("Toggle favorites", BuildFavRows(spells), delegate
			{
				MenuSystem.SetRows(BuildFavRows(spells));
			});
		}

		private static List<MenuRow> BuildFavRows(bool spells)
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Element element in KnownAbilities(spells))
			{
				Element e = element;
				bool fav = EClass.player.favAbility.Contains(e.id);
				rows.Add(new MenuRow
				{
					label = (fav ? "★ " : "☆ ") + e.source.GetName(),
					ability = e,
					closeOnAct = false,
					act = delegate
					{
						if (!EClass.player.favAbility.Remove(e.id))
						{
							EClass.player.favAbility.Add(e.id);
						}
					}
				});
			}
			return rows;
		}

		// the last spell/action the player cast: f repeats it (Elona spam-cast)
		private static Element lastCast;

		// f: ranged weapon first (native Fire), else repeat the last cast
		public static void FireSmart()
		{
			try
			{
				Thing ranged = pc.things.Find((Thing t) => t.isEquipped && t.trait is TraitToolRange);
				if (ranged != null)
				{
					InputRouter.QueueNative(EAction.Fire);
					return;
				}
			}
			catch
			{
			}
			if (lastCast != null && lastCast.Value != 0)
			{
				CastAbility(lastCast);
			}
			else
			{
				InputRouter.QueueNative(EAction.Fire); // native message/behavior
			}
		}

		// Cast honoring the spell's real TargetType (research: TargetType.cs,
		// Chara.UseAbility pt-flag loops the whole party = native multi-cast).
		internal static void CastAbility(Element e)
		{
			lastCast = e; // f repeats this
			Act act = (e as Act) ?? ACT.Create(e.source);
			TargetType tt = act.TargetType;
			// MANUAL mode (hold-v, or x on the row): no quick-cast, no hostility
			// pre-filters: the free cursor aims at anyone. The flag is consumed
			// HERE, one cast per arm — Close() must not touch it (Run() closes
			// the menu before the row action, so a reset there always won).
			if (ManualCastMode)
			{
				ManualCastMode = false;
				List<Chara> all = new List<Chara>();
				foreach (Chara cc in EClass._map.charas)
				{
					if (cc != pc && !cc.isDead && pc.CanSee(cc))
					{
						all.Add(cc);
					}
				}
				TargetMode.Begin(act.GetText() + " (manual)", all, delegate(Point p, Chara ch)
				{
					bool ok;
					if (tt.Range == TargetRange.Ground)
					{
						if (!SafeCanPerformAt(act, p))
						{
							Msg.SayRaw(act.GetText() + " cannot reach that spot. (No cost spent.)");
							return;
						}
						ok = ForceUse(act, null, p);
					}
					else if (ch != null)
					{
						if (!CanForcePerform(act, ch, ch.pos))
						{
							Msg.SayRaw(act.GetText() + " cannot target " + ch.Name + " (enemy-only or out of reach). (No cost spent.)");
							return;
						}
						ok = ForceUse(act, ch, ch.pos);
					}
					else if (p.Equals(pc.pos))
					{
						if (!CanForcePerform(act, pc, pc.pos))
						{
							Msg.SayRaw(act.GetText() + " cannot target you. (No cost spent.)");
							return;
						}
						ok = ForceUse(act, pc, pc.pos);
					}
					else
					{
						Msg.SayRaw("There is no one there.");
						return;
					}
					if (ok)
					{
						EClass.player.EndTurn();
					}
					// false = the game already printed the reason
				}, preferFoe: HostileAim(act));
				return;
			}
			// self-only: instant
			if (tt.Range == TargetRange.Self && !tt.ForceParty)
			{
				if (ForceUse(act, pc, pc.pos))
				{
					EClass.player.EndTurn();
				}
				return;
			}
			// party spells: cast on the whole party immediately (native multi-cast),
			// combat flow beats a member-picker
			if (tt.ForceParty || tt.Range == TargetRange.Party)
			{
				if (pc.UseAbility(act, null, null, pt: true))
				{
					EClass.player.EndTurn();
				}
				return;
			}
			// neighbor-only abilities (touch spells, interaction acts): a
			// remembered target standing in reach fires instantly — this is
			// what keeps them freely usable on your own people
			if (tt.Range == TargetRange.Neighbor)
			{
				Chara qn = TargetMode.QuickTarget(act);
				if (qn != null && CanForcePerform(act, qn, qn.pos))
				{
					if (ForceUse(act, qn, qn.pos))
					{
						TargetMode.Remember(qn);
						EClass.player.EndTurn();
					}
					return;
				}
				DirPrompt.Ask("Aim " + act.GetText() + " where? (wait key = your tile)", self: true, delegate(Point p)
				{
					// touch acts want the CHARA on that tile, not the bare
					// tile — with a null target many refuse and do nothing
					Chara ch = p.FirstVisibleChara();
					if (!CanForcePerform(act, ch, p))
					{
						Msg.SayRaw("You cannot aim there (blocked or invalid target). (No cost spent.)");
						return;
					}
					if (ForceUse(act, ch, p))
					{
						if (ch != null)
						{
							TargetMode.Remember(ch);
						}
						EClass.player.EndTurn();
					}
				});
				return;
			}
			// ground-targeted at range (projectiles like ice arrow): aim at
			// characters' tiles via the remembered target / targeting mode
			if (tt.Range == TargetRange.Ground)
			{
				Chara quickG = TargetMode.QuickTarget(act);
				// only quick-fire when the game can actually perform there;
				// otherwise fall into the aim cursor (which names the spell)
				if (quickG != null && SafeCanPerformAt(act, quickG.pos))
				{
					// a false return means the game already said why — stay quiet
					if (ForceUse(act, null, quickG.pos))
					{
						TargetMode.Remember(quickG);
						EClass.player.EndTurn();
					}
					return;
				}
				List<Chara> spots = new List<Chara>();
				foreach (Chara c in EClass._map.charas)
				{
					if (c != pc && !c.isDead && pc.CanSee(c) && SafeCanPerformAt(act, c.pos))
					{
						spots.Add(c);
					}
				}
				TargetMode.Begin(act.GetText(), spots, delegate(Point p, Chara c)
				{
					if (!SafeCanPerformAt(act, p))
					{
						Msg.SayRaw(act.GetText() + " cannot reach that spot. (No cost spent.)");
						return;
					}
					// UseAbility failures explain themselves; no double messages
					if (ForceUse(act, null, p))
					{
						EClass.player.EndTurn();
					}
				}, preferFoe: HostileAim(act));
				return;
			}
			// chara-targeted: remembered target fires INSTANTLY (Elona flow);
			// otherwise the in-world targeting mode (dim + cycle), no window.
			// The quick shot only fires when the game can perform on that
			// target — a refused one (ally for an enemy-only spell) used to
			// eat the cost with no visible effect. Refusal opens the cursor.
			Chara quick = TargetMode.QuickTarget(act);
			if (quick != null && CanForcePerform(act, quick, quick.pos))
			{
				// force-cast, friend or foe; silent on false (the game speaks)
				if (ForceUse(act, quick, quick.pos))
				{
					TargetMode.Remember(quick);
					EClass.player.EndTurn();
				}
				return;
			}
			TargetMode.Begin(act.GetText(), CastCandidates(act, tt), delegate(Point p, Chara c)
			{
				if (c == null)
				{
					Msg.SayRaw("There is no one there.");
				}
				else if (!CanForcePerform(act, c, c.pos))
				{
					Msg.SayRaw(act.GetText() + " cannot target " + c.Name + " (enemy-only or out of reach). (No cost spent.)");
				}
				else if (ForceUse(act, c, c.pos))
				{
					EClass.player.EndTurn();
				}
			}, preferFoe: HostileAim(act));
		}

		// a hostile aim starts its cursor on a FOE, never the ally lock
		private static bool HostileAim(Act act)
		{
			try
			{
				return act.IsHostileAct || act.TargetType.CanOnlyTargetEnemy;
			}
			catch
			{
				return true;
			}
		}

		private static bool SafeCanPerformAt(Act act, Point p)
		{
			return CanForcePerform(act, null, p);
		}

		// An explicit aim is consent (Elona rule): while WE check and cast,
		// the shift-only "negative ability on your own" config gate is lifted
		// AND enemy-only targeting accepts anyone (Patch_ForceFriendlyFire
		// reads this flag). The pre-check matters because UseAbility pays the
		// cost BEFORE Perform re-runs CanPerform — without it a refused
		// target silently ate the mana/sp.
		internal static bool ForceFriendlyFire;

		private static bool CanForcePerform(Act act, Chara tc, Point p)
		{
			var game = EClass.core.config.game;
			bool gate = game.shiftToUseNegativeAbilityOnSelf;
			game.shiftToUseNegativeAbilityOnSelf = false;
			ForceFriendlyFire = true;
			try
			{
				return act.CanPerform(pc, tc, p ?? tc?.pos);
			}
			catch
			{
				return false;
			}
			finally
			{
				game.shiftToUseNegativeAbilityOnSelf = gate;
				ForceFriendlyFire = false;
			}
		}

		private static bool ForceUse(Act act, Chara tc, Point p)
		{
			var game = EClass.core.config.game;
			bool gate = game.shiftToUseNegativeAbilityOnSelf;
			game.shiftToUseNegativeAbilityOnSelf = false;
			ForceFriendlyFire = true;
			try
			{
				return pc.UseAbility(act, tc, p);
			}
			finally
			{
				game.shiftToUseNegativeAbilityOnSelf = gate;
				ForceFriendlyFire = false;
			}
		}

		private static List<Chara> CastCandidates(Act act, TargetType tt)
		{
			List<Chara> targets = new List<Chara>();
			foreach (Chara c in EClass._map.charas)
			{
				if (c.isDead || !pc.CanSee(c))
				{
					continue;
				}
				if (c == pc && !tt.CanSelectSelf)
				{
					continue;
				}
				if (tt.CanOnlyTargetEnemy && !pc.IsHostile(c))
				{
					continue;
				}
				if (!act.IsValidTC(c) || !act.CanPerform(pc, c, c.pos))
				{
					continue;
				}
				targets.Add(c);
			}
			targets.Sort((Chara a, Chara b) => pc.pos.Distance(a.pos) - pc.pos.Distance(b.pos));
			return targets;
		}

		// classic '*': set or change the remembered target without acting.
		// YOURSELF is a valid ally lock (solo buffing); Space cycles to you.
		public static void PickTarget()
		{
			List<Chara> targets = new List<Chara> { pc };
			foreach (Chara c in EClass._map.charas)
			{
				if (c != pc && !c.isDead && pc.CanSee(c) && pc.pos.Distance(c.pos) <= 12)
				{
					targets.Add(c);
				}
			}
			TargetMode.Begin("Target", targets, delegate(Point p, Chara c)
			{
				if (c == null)
				{
					Msg.SayRaw("No one to remember there.");
				}
				else
				{
					TargetMode.Remember(c);
					bool foe = false;
					try
					{
						foe = pc.IsHostile(c) && !c.IsPCParty;
					}
					catch
					{
					}
					// two locks live side by side: hostile casts use the enemy
					// lock (or auto-acquire), buffs/heals the ally lock; the
					// Swap key aims one cast at the opposite lock
					Msg.SayRaw((foe ? "Enemy lock: " : "Ally lock: ") + (c == pc ? "yourself" : c.Name) + ".");
				}
			});
		}

		// config macros: cast a comma-separated ability sequence (buff chains)
		public static void RunMacro(string csv)
		{
			if (string.IsNullOrWhiteSpace(csv))
			{
				Msg.SayRaw("This macro is empty (set it in the config).");
				return;
			}
			foreach (string raw in csv.Split(','))
			{
				string alias = raw.Trim();
				if (alias.Length == 0)
				{
					continue;
				}
				Element e = pc.elements.GetElement(alias);
				if (e == null || e.Value == 0)
				{
					Msg.SayRaw("You don't know " + alias + ".");
					continue;
				}
				Act act = (e as Act) ?? ACT.Create(e.source);
				TargetType tt = act.TargetType;
				bool ok;
				if (tt.ForceParty || tt.Range == TargetRange.Party)
				{
					ok = pc.UseAbility(act, null, null, pt: true);
				}
				else if (tt.Range == TargetRange.Self)
				{
					ok = pc.UseAbility(act, pc, pc.pos);
				}
				else if (pc.enemy != null && !pc.enemy.isDead && pc.CanSee(pc.enemy) && act.CanPerform(pc, pc.enemy, pc.enemy.pos))
				{
					ok = pc.UseAbility(act, pc.enemy, pc.enemy.pos);
				}
				else
				{
					Msg.SayRaw(act.GetText() + " needs a target, skipped.");
					continue;
				}
				if (ok)
				{
					EClass.player.EndTurn();
				}
			}
		}

		// ---------- simple toggles ----------

		// Elona x: the inventory BROWSER. period/comma cycle the shown
		// inventory: you -> each party member -> home stock (containers).
		// Enter = stat card for your own things, count-spinner take elsewhere.
		private static int browseSrc;
		private static bool browseHomeFlat; // home view: containers vs one flat list

		public static void ExamineInventory()
		{
			MenuCycle.Set("items", 0);
			browseSrc = 0;
			browseHomeFlat = false;
			ShowBrowser(replaceScreen: false);
		}

		private static List<(string name, Chara owner)> BrowserSources()
		{
			List<(string, Chara)> s = new List<(string, Chara)> { ("You", pc) };
			foreach (Chara c in EClass.pc.party.members)
			{
				if (c != pc && !c.isDead)
				{
					s.Add((c.Name, c));
				}
			}
			if (EClass._zone.IsPCFaction || EClass._zone is Zone_Tent)
			{
				s.Add(("Home stock", null));
			}
			return s;
		}

		private static void ShowBrowser(bool replaceScreen)
		{
			List<(string name, Chara owner)> sources = BrowserSources();
			browseSrc = ((browseSrc % sources.Count) + sources.Count) % sources.Count;
			(string name, Chara owner) src = sources[browseSrc];
			List<MenuRow> rows = BuildBrowserRows(src.owner);
			if (Plugin.DebugLog)
			{
				Plugin.Log?.LogInfo("ElonaKeys browser: source=" + src.name + " rows=" + rows.Count + " (of " + sources.Count + " sources)");
			}
			string title = "Inventory - " + src.name + "  (. or , = next/prev inventory)";
			if (rows.Count == 0)
			{
				rows.Add(new MenuRow { label = "<color=#8f8f7a>(empty)</color>" });
			}
			// free-space readout in the footer (party members included)
			if (src.owner != null)
			{
				MenuSystem.NextFooterExtra = SlotText(src.owner);
			}
			// period/comma hop You / party members / Home stock. NOT Tab (the
			// browser sits in the items family; Tab must keep hopping windows)
			// and NOT z/x (sort/examine, which source-cycling used to eat).
			int count = sources.Count;
			MenuSystem.NextOnSwap = delegate(int dir)
			{
				browseSrc = ((browseSrc + dir) % count + count) % count;
				browseHomeFlat = false; // fresh home visit starts on containers
				ShowBrowser(replaceScreen: true);
			};
			MenuSystem.Show(title, rows, delegate
			{
				MenuSystem.SetRows(BuildBrowserRows(src.owner));
			}, keepLayers: false, replace: replaceScreen);
		}

		private static List<MenuRow> BuildBrowserRows(Chara owner)
		{
			List<MenuRow> rows = new List<MenuRow>();
			if (owner == pc)
			{
				foreach (Thing thing in pc.things.List((Thing t) => !(t.trait is TraitAbility), onlyAccessible: true))
				{
					Thing t = thing;
					bool held = t == pc.held;
					rows.Add(new MenuRow
					{
						label = (held ? "(held) " : "") + t.Name + (t.c_isImportant ? " <color=#c9a64f>★</color>" : "") + (t.isEquipped ? " <size=12><color=#90d090>(equipped)</color></size>" : "") + Quickbind.TagForItem(t.id),
						icon = t,
						pinTop = held,
						rightText = WeightText(t.ChildrenAndSelfWeight),
						sortWeight = t.ChildrenAndSelfWeight,
						closeOnAct = false,
						act = delegate
						{
							EClass.ui.AddLayerDontCloseOthers<LayerInfo>().SetThing(t);
						},
						// numpad0 = take it into your hands (safe subset only)
						altAct = (!t.isEquipped && t.trait.CanBeHeld && !held) ? ((Action)delegate
						{
							ToggleHold(t);
						}) : null
					});
				}
				return rows;
			}
			// HOME: list the CONTAINERS (open one = full take/put/settings via
			// the container menu, so things go back into the chest they came
			// from), with the old flat everything-list one row away — 83 pages
			// of loose items was unusable
			if (owner == null && !browseHomeFlat)
			{
				rows.Add(new MenuRow
				{
					label = "[ All items in one list... ]",
					pinTop = true,
					closeOnAct = false,
					act = delegate
					{
						browseHomeFlat = true;
						ShowBrowser(replaceScreen: true);
					}
				});
				foreach (Thing thing in EClass._map.things)
				{
					Thing t = thing;
					if (t != null && !t.isDestroyed && t.IsInstalled && t.trait.CanOpenContainer && t.c_lockLv == 0)
					{
						rows.Add(new MenuRow
						{
							label = t.Name + "  <size=13><color=#8f8f7a>[" + t.things.Count + "/" + t.things.MaxCapacity + "]</color></size>",
							icon = t,
							rightText = WeightText(t.ChildrenAndSelfWeight),
							sortWeight = t.ChildrenAndSelfWeight,
							sortCount = t.things.Count,
							closeOnAct = false,
							act = delegate { ContainerMenu(t); }
						});
					}
				}
				return rows;
			}
			// party member or home stock: browse + take (count spinner)
			List<Thing> pool = new List<Thing>();
			if (owner != null)
			{
				pool.AddRange(owner.things.List((Thing t) => !(t.trait is TraitAbility) && !t.isEquipped, onlyAccessible: true));
			}
			else
			{
				foreach (Thing t in EClass._map.Stocked.things)
				{
					if (t != null && !t.isDestroyed && !t.isNPCProperty)
					{
						pool.Add(t);
					}
				}
			}
			foreach (Thing thing in pool)
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = t.Name,
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
					closeOnAct = false,
					maxAmount = Mathf.Max(1, t.Num),
					onAmount = delegate(int n)
					{
						try
						{
							if (Plugin.DebugLog)
							{
								Plugin.Log?.LogInfo("ElonaKeys browser take: " + t.Name + " x" + n + " from " + (owner != null ? owner.Name : "home stock") + " (uid " + t.uid + ")");
							}
							SafeTake(t, delegate
							{
								if (BackpackFull(t))
								{
									return;
								}
								Thing taken = (n >= t.Num) ? t : t.Split(n);
								pc.Pick(taken);
							});
						}
						catch (Exception ex)
						{
							Plugin.Log?.LogError("ElonaKeys browser take failed: " + ex);
						}
					}
				});
			}
			return rows;
		}
	}
}
