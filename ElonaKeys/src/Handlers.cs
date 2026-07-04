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
					label = slotName + ":  " + ((eq != null) ? DetailName(eq) : "-"),
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
					label = item.GetTextContext(plan.HasMultipleTargets).Replace("\n", " "),
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

		public static void SearchHere()
		{
			PlanMenu("Actions here", pc.pos.Copy(), "There is nothing to do here.");
		}

		public static void Open()
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Thing thing in pc.things.List((Thing t) => Safe((Thing x) => x.trait.CanOpenContainer || (x.trait is TraitCrafter && x.trait.CanUse(pc)), t), onlyAccessible: true))
			{
				Thing t = thing;
				rows.Add(new MenuRow
				{
					label = (t.trait.CanOpenContainer ? "Open: " : "Use: ") + t.Name,
					icon = t,
					act = delegate
					{
						if (t.trait.CanOpenContainer)
						{
							(t.trait as TraitContainer)?.TryOpen();
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
						rows.Add(new MenuRow
						{
							label = "Open: " + t.Name + " (nearby)",
							icon = t,
							act = delegate
							{
								if (t.c_lockLv > 0)
								{
									pc.SetAIImmediate(new AI_OpenLock { target = t });
								}
								else
								{
									(t.trait as TraitContainer)?.TryOpen();
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

		// Classic 'z' action wheel: radial circle by default, window as fallback.
		public static void Wheel()
		{
			if (Plugin.WheelStyle != "list")
			{
				RadialWheel.Toggle(WheelVerbList());
				return;
			}
			WheelWindow();
		}

		private static readonly Dictionary<string, (string label, Action act)> wheelVerbs =
			new Dictionary<string, (string, Action)>(StringComparer.OrdinalIgnoreCase)
		{
			{ "Eat", ("Eat", Eat) },
			{ "Quaff", ("Quaff", Quaff) },
			{ "Read", ("Read", ReadItem) },
			{ "Zap", ("Zap", Zap) },
			{ "Cast", ("Cast", Cast) },
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
			{ "Inventory", ("Inventory", ExamineInventory) }
		};

		private static List<(string, Action)> WheelVerbList()
		{
			List<(string, Action)> list = new List<(string, Action)>();
			foreach (string raw in Plugin.WheelVerbs.Split(','))
			{
				string name = raw.Trim();
				if (name.Length > 0 && wheelVerbs.TryGetValue(name, out (string label, Action act) v))
				{
					list.Add((v.label, v.act));
				}
			}
			if (list.Count == 0)
			{
				foreach (KeyValuePair<string, (string label, Action act)> kv in wheelVerbs)
				{
					list.Add((kv.Value.label, kv.Value.act));
				}
			}
			return list;
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

		// ---------- simple toggles ----------

		public static void Cast()
		{
			EClass.ui.ToggleAbility();
		}

		public static void ExamineInventory()
		{
			EClass.ui.ToggleInventory();
		}
	}
}
