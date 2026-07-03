using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElonaKeys
{
	// Implementations of each classic Elona action on top of Elin's systems.
	// All menus go through MenuSystem (paginated, keyboard-navigable).
	public static class Handlers
	{
		private static Chara pc => EClass.pc;

		// ---------- shared plumbing ----------

		private static void ItemMenu(string title, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg, bool refresh = false, Func<Thing, string> label = null, List<MenuRow> extraRows = null, string category = null)
		{
			List<Thing> things = Matching(filter, category);
			if (things.Count == 0 && (extraRows == null || extraRows.Count == 0))
			{
				Msg.SayRaw(emptyMsg);
				return;
			}
			if (category == null && Plugin.MenuCategories && things.Count > Plugin.CategoryThreshold)
			{
				CategoryMenu(title, things, filter, onSelect, emptyMsg, refresh, label, extraRows);
				return;
			}
			List<MenuRow> rows = BuildRows(things, onSelect, label, refresh, extraRows);
			if (rows.Count == 0)
			{
				Msg.SayRaw(emptyMsg);
				return;
			}
			if (refresh)
			{
				string cat = category;
				MenuSystem.Show(title, rows, delegate
				{
					MenuSystem.SetRows(BuildRows(Matching(filter, cat), onSelect, label, refresh: true, null));
				});
			}
			else
			{
				MenuSystem.Show(title, rows);
			}
		}

		private static List<Thing> Matching(Func<Thing, bool> filter, string category)
		{
			// TraitAbility things are hotbar spell placeholders (TraitAbility :
			// TraitCatalyst : TraitTool), not real inventory items: never list them.
			List<Thing> things = pc.things.List((Thing t) => !(t.trait is TraitAbility) && Safe(filter, t), onlyAccessible: true);
			if (!string.IsNullOrEmpty(category))
			{
				things.RemoveAll((Thing t) => CategoryName(t) != category);
			}
			return things;
		}

		private static List<MenuRow> BuildRows(List<Thing> things, Action<Thing> onSelect, Func<Thing, string> label, bool refresh, List<MenuRow> extraRows)
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
					label = (label != null) ? label(t) : t.Name,
					icon = t,
					closeOnAct = !refresh,
					act = delegate { onSelect(t); }
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

		private static void CategoryMenu(string title, List<Thing> things, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg, bool refresh, Func<Thing, string> label, List<MenuRow> extraRows)
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
				act = delegate { ItemMenu(title, filter, onSelect, emptyMsg, refresh, label, null, ""); }
			});
			foreach (KeyValuePair<string, int> kv in counts)
			{
				string cat = kv.Key;
				rows.Add(new MenuRow
				{
					label = cat + " (" + kv.Value + ")",
					act = delegate { ItemMenu(title + " - " + cat, filter, onSelect, emptyMsg, refresh, label, null, cat); }
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

		// Rows for every act with the given DynamicAct id on nearby tiles
		// (e.g. "actDrink" finds wells and fountains through the game's own logic).
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
			else if (rows.Count == 1)
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

		public static void Drop()
		{
			ItemMenu("Drop what?", (Thing t) => !t.isEquipped,
				delegate(Thing t) { pc.DropThing(t); },
				"You have nothing to drop.",
				refresh: true);
		}

		public static void Wear()
		{
			ItemMenu("Wear or wield what?", (Thing t) => t.IsEquipmentOrRanged,
				delegate(Thing t)
				{
					if (t.isEquipped)
					{
						pc.body.Unequip(t);
					}
					else if (!pc.body.Equip(t))
					{
						Msg.SayRaw("You cannot equip that.");
					}
				},
				"You have nothing to equip.",
				refresh: true,
				label: (Thing t) => t.isEquipped ? (t.Name + " <size=12>(equipped)</size>") : t.Name);
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
				delegate(Thing t)
				{
					if (pc.enemy != null && pc.CanSee(pc.enemy) && !pc.enemy.isDead)
					{
						ActThrow.Throw(pc, pc.enemy.pos, t);
						EClass.player.EndTurn();
						return;
					}
					DirPrompt.Ask("Throw in which direction?", self: false, delegate(Point p)
					{
						ActThrow.Throw(pc, p, t);
						EClass.player.EndTurn();
					});
				},
				"You have nothing to throw.");
		}

		// Classic 'A': load compatible ammo into your ranged weapon.
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

		// Classic 'g': pick up what the game itself considers pickable here,
		// falling back to loose things on the tile.
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

		// Classic 's': everything you could do on your own tile (search spots,
		// harvest, dig treasure, gather...) as a menu.
		public static void SearchHere()
		{
			ActPlan plan = BuildPlan(pc.pos.Copy());
			if (!plan.HasAct)
			{
				Msg.SayRaw("There is nothing to do here.");
			}
			else
			{
				plan.ShowContextMenu();
			}
		}

		// Classic 'o': containers and crafting kits, in inventory and nearby.
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

		// Classic 'C': open or close a door in a chosen direction.
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

		public static void Bash()
		{
			DirPrompt.Ask("Bash in which direction?", self: false, delegate(Point p)
			{
				ActBash act = new ActBash();
				if (act.Perform(pc, null, p))
				{
					EClass.player.EndTurn();
				}
				else
				{
					Msg.SayRaw("There is nothing there to bash.");
				}
			});
		}

		public static void Dig()
		{
			// auto-hold a digging tool first, like Elona
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
							act = delegate { LayerDragGrid.CreateGive(target); }
						});
					}
				}
			}
			RunRows("Give to whom?", rows, "There is nobody here to give things to.");
		}

		public static void Interact()
		{
			DirPrompt.Ask("Interact with which direction? (press the wait key for yourself)", self: true, delegate(Point p)
			{
				ActPlan plan = BuildPlan(p);
				if (!plan.HasAct)
				{
					Msg.SayRaw("There is nothing to interact with.");
				}
				else
				{
					plan.ShowContextMenu();
				}
			});
		}

		// Party access: character sheets and inventories of your party members.
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
					label = c.Name + " - Inventory",
					act = delegate { LayerInventory.CreateContainerAlly(c, c); }
				});
			}
			RunRows("Party", rows, "You have no party members with you.");
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
