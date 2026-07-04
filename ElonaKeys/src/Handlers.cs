using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElonaKeys
{
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
				rows.AddRange(extraRows);
			}
			foreach (Thing thing in things)
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = (label != null) ? label(t) : DetailName(t),
					icon = t,
					rightText = WeightText(t.ChildrenAndSelfWeight),
					sortWeight = t.ChildrenAndSelfWeight,
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
			Dictionary<string, int> counts = new Dictionary<string, int>();
			foreach (Thing t in things)
			{
				string c = CategoryName(t);
				counts.TryGetValue(c, out int n);
				counts[c] = n + 1;
			}
			List<MenuRow> rows = new List<MenuRow>();
			if (extraRows != null)
			{
				rows.AddRange(extraRows);
			}
			rows.Add(new MenuRow
			{
				label = "All items (" + things.Count + ")",
				closeOnAct = false, // keep this window in the back-stack for shift
				act = delegate { ItemMenu(title, filter, onSelect, emptyMsg, refresh, label, null, "", amount, onAmount); }
			});
			foreach (KeyValuePair<string, int> kv in counts)
			{
				string cat = kv.Key;
				rows.Add(new MenuRow
				{
					label = cat + " (" + kv.Value + ")",
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

		internal static ActPlan BuildPlan(Point p, ActInput input = ActInput.AllAction)
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
			ItemMenu("Eat what?", (Thing t) => t.trait.CanEat(pc) && !t.isEquipped,
				delegate(Thing t) { t.DoAct(new AI_Eat { cook = false, target = t }); },
				"You have nothing to eat.");
		}

		public static void Quaff()
		{
			ItemMenu("Quaff what?", (Thing t) => t.trait.CanDrink(pc),
				delegate(Thing t) { t.DoAct(new AI_Drink { target = t }); },
				"You have nothing to drink.",
				refresh: false, label: null, extraRows: TileActRows("actDrink", "Drink: "));
		}

		public static void ReadItem()
		{
			ItemMenu("Read what?", (Thing t) => t.trait.CanRead(pc),
				delegate(Thing t) { t.DoAct(new AI_Read { target = t }); },
				"You have nothing to read.");
		}

		public static void Zap()
		{
			ItemMenu("Zap which rod?", (Thing t) => t.trait is TraitRod,
				delegate(Thing t)
				{
					DirPrompt.Ask("Zap in which direction? (press the wait key to zap yourself)", self: true, delegate(Point p)
					{
						ActZap act = new ActZap { trait = (TraitRod)t.trait };
						if (act.Perform(pc, null, p))
						{
							EClass.player.EndTurn();
						}
					});
				},
				"You have no rods.");
		}

		// Elona drop: pick item, spinner picks the count, menu stays open.
		public static void Drop()
		{
			ItemMenu("Drop what?", (Thing t) => !t.isEquipped,
				null,
				"You have nothing to drop.",
				refresh: true, label: null, extraRows: null, category: null,
				amount: (Thing t) => t.Num,
				onAmount: delegate(Thing t, int n) { pc.DropThing(t, n); });
		}

		// Elona equipment window: body slots with what's worn, pick a slot to
		// change it. Footer stats live in the caption.
		public static void Wear()
		{
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
				return EClass.sources.elements.map[slot.elementId].GetName();
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
			if (slot.thing != null)
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

		public static void HoldTool()
		{
			ItemMenu("Hold which tool?", (Thing t) => t.trait is TraitTool && !(t.trait is TraitCatalyst) && !t.isEquipped,
				delegate(Thing t) { pc.HoldCard(t); },
				"You have no tools.");
		}

		public static void Use()
		{
			ItemMenu("Use what?", (Thing t) => t.trait.CanUse(pc),
				delegate(Thing t) { t.trait.OnUse(pc); },
				"You have nothing usable.");
		}

		public static void Hold()
		{
			ItemMenu("Hold what in your hands?", (Thing t) => t.trait.CanBeHeld && !t.isEquipped,
				delegate(Thing t) { pc.HoldCard(t); },
				"You have nothing you can hold.");
		}

		public static void Blend()
		{
			ItemMenu("Dip or blend with what?", (Thing t) => t.trait.IsBlendBase,
				delegate(Thing t) { LayerDragGrid.Create(new InvOwnerBlend(t)); },
				"You have nothing to blend with (potions, blessed water and other liquids).");
		}

		public static void Throw()
		{
			ItemMenu("Throw what?", (Thing t) => !t.isEquipped && t.trait.CanBeHeld,
				delegate(Thing t) { ThrowTargetMenu(t); },
				"You have nothing to throw.",
				refresh: true); // keeps the item list in the back-stack for shift
		}

		private static void ThrowTargetMenu(Thing t)
		{
			List<MenuRow> rows = new List<MenuRow>();
			List<Chara> targets = new List<Chara>();
			foreach (Chara c in EClass._map.charas)
			{
				if (c != pc && !c.isDead && pc.CanSee(c) && pc.pos.Distance(c.pos) <= 8)
				{
					targets.Add(c);
				}
			}
			targets.Sort((Chara a, Chara b) => pc.pos.Distance(a.pos) - pc.pos.Distance(b.pos));
			foreach (Chara chara in targets)
			{
				Chara c = chara;
				string tag = pc.IsHostile(c) ? " <color=#ff8080>(hostile)</color>" : "";
				rows.Add(new MenuRow
				{
					label = "Throw at " + c.Name + " (" + pc.pos.Distance(c.pos) + ")" + tag,
					act = delegate
					{
						ActThrow.Throw(pc, c.pos, t);
						EClass.player.EndTurn();
					}
				});
			}
			rows.Add(new MenuRow
			{
				label = "Pick a direction...",
				act = delegate
				{
					DirPrompt.Ask("Throw in which direction?", self: false, delegate(Point p)
					{
						ActThrow.Throw(pc, p, t);
						EClass.player.EndTurn();
					});
				}
			});
			MenuSystem.Show("Throw " + t.Name + " at?", rows);
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

		public static void Get()
		{
			ActPlan plan = BuildPlan(pc.pos.Copy());
			List<Thing> picked = new List<Thing>();
			foreach (Thing t in plan.listPick)
			{
				picked.Add(t);
			}
			if (picked.Count == 0)
			{
				foreach (Thing t in pc.pos.Things)
				{
					if (!t.IsInstalled && t.trait.CanBeHeld)
					{
						picked.Add(t);
					}
				}
			}
			if (picked.Count == 0)
			{
				Msg.SayRaw("There is nothing here to pick up.");
				return;
			}
			foreach (Thing t in picked)
			{
				pc.Pick(t);
			}
		}

		private static void PlanMenu(string title, Point p, string emptyMsg)
		{
			ActPlan plan = BuildPlan(p);
			List<MenuRow> rows = new List<MenuRow>();
			foreach (ActPlan.Item i in plan.list)
			{
				ActPlan.Item item = i;
				rows.Add(new MenuRow
				{
					label = ContextLabel(item, plan.HasMultipleTargets),
					icon = item.tc as Thing,
					act = delegate
					{
						if (item.Perform())
						{
							EClass.player.EndTurn();
						}
					}
				});
			}
			RunRows(title, rows, emptyMsg);
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
			PlanMenu("Actions here", pc.pos.Copy(), "There is nothing to do here.");
		}

		// Elona Space: everything doable on and around you, one action fires instantly.
		public static void ContextAll()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Point point in Nearby())
			{
				Point p = point;
				ActPlan plan = BuildPlan(p);
				foreach (ActPlan.Item i in plan.list)
				{
					ActPlan.Item item = i;
					rows.Add(new MenuRow
					{
						label = ContextLabel(item, forceName: true),
						icon = item.tc as Thing,
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
			RunRows("Around you", rows, "There is nothing to interact with here.");
		}

		public static void Open()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in pc.things.List((Thing t) => Safe((Thing x) => x.trait.CanOpenContainer || (x.trait is TraitCrafter && x.trait.CanUse(pc)), t), onlyAccessible: true))
			{
				Thing t = thing;
				bool isContainer = t.trait.CanOpenContainer;
				rows.Add(new MenuRow
				{
					label = (isContainer ? "Open: " : "Use: ") + t.Name,
					icon = t,
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
							label = "Open: " + t.Name + " (nearby)" + (locked ? " <color=#c08080>(locked)</color>" : ""),
							icon = t,
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
			RunRows("Open what?", rows, "There is nothing to open here.");
		}

		// keyboard take/put for any container (chests, bags, boxes)
		internal static void ContainerMenu(Thing container)
		{
			MenuSystem.Show(container.Name, BuildContainerRows(container), delegate
			{
				MenuSystem.SetRows(BuildContainerRows(container));
			});
		}

		private static List<MenuRow> BuildContainerRows(Thing container)
		{
			List<MenuRow> rows = new List<MenuRow>();
			rows.Add(new MenuRow
			{
				label = "[ Put items in... ]",
				closeOnAct = false,
				act = delegate { PutMenu(container); }
			});
			rows.Add(new MenuRow
			{
				label = "[ Container settings... ]",
				closeOnAct = false,
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
						if (dest.things.IsFull())
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
					label = name + ":  " + (value ? "<color=#70a070>ON</color>" : "<color=#a07070>OFF</color>"),
					closeOnAct = false,
					act = flip
				});
			}
			Toggle("Only rottable items (cooler/fridge)", sd.onlyRottable, delegate { sd.onlyRottable = !sd.onlyRottable; });
			Toggle("Hide/refuse rotten items", sd.noRotten, delegate { sd.noRotten = !sd.noRotten; });
			Toggle("Always sort", sd.alwaysSort, delegate { sd.alwaysSort = !sd.alwaysSort; });
			rows.Add(new MenuRow
			{
				label = "Distribution priority: " + sd.priority + "  (pick a new value)",
				closeOnAct = false,
				maxAmount = 20,
				onAmount = delegate(int n) { sd.priority = n; }
			});
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
					if (container.things.IsFull())
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
				pc.HoldCard(tool);
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
						rows.Add(new MenuRow
						{
							label = "Give to: " + c.Name,
							closeOnAct = false, // shift returns to this list
							act = delegate { GiveTo(target); }
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
					act = delegate { EClass.ui.AddLayer<LayerChara>().SetChara(c); }
				});
				rows.Add(new MenuRow
				{
					label = c.Name + " - Items",
					closeOnAct = false,
					act = delegate { PartyInventory(c); }
				});
			}
			RunRows("Party", rows, "You have no party members with you.");
		}

		private static void PartyInventory(Chara c)
		{
			MenuSystem.Show(c.Name + "'s items", BuildPartyRows(c), delegate
			{
				MenuSystem.SetRows(BuildPartyRows(c));
			});
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
		public static void Cast()
		{
			AbilityMenu(spells: true, bucket: null, favOnly: false);
		}

		public static void ActionList()
		{
			AbilityMenu(spells: false, bucket: null, favOnly: false);
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
			if (bucket == null && !favOnly)
			{
				rows.Add(new MenuRow
				{
					label = "[ ★ Favorites ]",
					closeOnAct = false,
					act = delegate { AbilityMenu(spells, null, favOnly: true); }
				});
				rows.Add(new MenuRow
				{
					label = "[ Filter by type... ]",
					closeOnAct = false,
					act = delegate { FilterMenu(spells); }
				});
				rows.Add(new MenuRow
				{
					label = "[ Manage favorites... ]",
					closeOnAct = false,
					act = delegate { FavoriteMenu(spells); }
				});
			}
			foreach (Element element in known)
			{
				Element e = element;
				if (favOnly && !EClass.player.favAbility.Contains(e.id))
				{
					continue;
				}
				if (bucket != null && TypeBucket(e) != bucket)
				{
					continue;
				}
				rows.Add(AbilityRow(e, spells));
			}
			if (bucket == null && !favOnly)
			{
				rows.Add(new MenuRow
				{
					label = "(open the ability window...)",
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

		private static MenuRow AbilityRow(Element e, bool spells)
		{
			bool fav = EClass.player.favAbility.Contains(e.id);
			string cost = (e.source.cost != null && e.source.cost.Length > 0 && e.source.cost[0] > 0)
				? (e.source.cost[0] + (spells ? " mp" : " sp")) : "";
			return new MenuRow
			{
				label = (fav ? "★ " : "") + e.source.GetName() + " <size=13><color=#8f8f7a>" + TypeBucket(e) + "</color></size>",
				rightText = cost,
				actSrc = e.source,
				act = delegate { CastAbility(e); }
			};
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

		// Cast honoring the spell's real TargetType (research: TargetType.cs,
		// Chara.UseAbility pt-flag loops the whole party = native multi-cast).
		private static void CastAbility(Element e)
		{
			Act act = (e as Act) ?? ACT.Create(e.source);
			TargetType tt = act.TargetType;
			// self-only: instant
			if (tt.Range == TargetRange.Self && !tt.ForceParty)
			{
				if (pc.UseAbility(act, pc, pc.pos))
				{
					EClass.player.EndTurn();
				}
				return;
			}
			// party spells: native multi-cast, plus individual picks
			if (tt.ForceParty || tt.Range == TargetRange.Party)
			{
				List<MenuRow> prows = new List<MenuRow>();
				prows.Add(new MenuRow
				{
					label = "Cast on the whole party",
					act = delegate
					{
						if (pc.UseAbility(act, null, null, pt: true))
						{
							EClass.player.EndTurn();
						}
					}
				});
				foreach (Chara chara in pc.party.members)
				{
					Chara m = chara;
					prows.Add(new MenuRow
					{
						label = m.IsPC ? "Yourself" : m.Name,
						act = delegate
						{
							if (pc.UseAbility(act, m, m.pos))
							{
								EClass.player.EndTurn();
							}
						}
					});
				}
				MenuSystem.Show(act.GetText() + " on?", prows);
				return;
			}
			// ground-targeted: iso-aware direction (self allowed when valid)
			if (tt.Range == TargetRange.Ground || tt.Range == TargetRange.Neighbor)
			{
				DirPrompt.Ask("Aim " + act.GetText() + " where? (wait key = your tile)", self: true, delegate(Point p)
				{
					if (pc.UseAbility(act, null, p))
					{
						EClass.player.EndTurn();
					}
					else
					{
						Msg.SayRaw("You cannot aim there.");
					}
				});
				return;
			}
			// chara-targeted: menu of valid targets, range/LOS enforced by CanPerform
			List<MenuRow> rows = new List<MenuRow>();
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
			foreach (Chara chara in targets)
			{
				Chara c = chara;
				string tag = pc.IsHostile(c) ? " <color=#ff8080>(hostile)</color>" : (c.IsPCParty ? " <color=#80c080>(party)</color>" : "");
				rows.Add(new MenuRow
				{
					label = (c == pc ? "Yourself" : c.Name) + " (" + pc.pos.Distance(c.pos) + ")" + tag,
					act = delegate
					{
						if (pc.UseAbility(act, c, c.pos))
						{
							EClass.player.EndTurn();
						}
					}
				});
			}
			if (rows.Count == 0)
			{
				Msg.SayRaw("No valid target in range for " + act.GetText() + ".");
				return;
			}
			MenuSystem.Show(act.GetText() + " at?", rows);
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

		public static void ExamineInventory()
		{
			EClass.ui.ToggleInventory();
		}
	}
}
