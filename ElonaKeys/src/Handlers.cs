using System;
using System.Collections.Generic;

namespace ElonaKeys
{
	// Implementations of each classic Elona action on top of Elin's systems.
	public static class Handlers
	{
		private static Chara pc => EClass.pc;

		private const string Letters = "abcdefghijklmnopqrstuvwxyz";

		private class Row
		{
			public string label;
			public Thing icon;
			public Action act;
		}

		// ---------- Elona-style scrollable list menu ----------

		private static void ShowMenu(string title, List<Row> rows)
		{
			LayerList layer = EClass.ui.AddLayer<LayerList>();
			layer.SetSize(560f);
			layer.windows[0].SetCaption(title);
			int index = 0;
			layer.SetList2(rows, (Row r) => r.label, delegate(Row r, ItemGeneral b)
			{
				try
				{
					r.act();
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys action failed: " + ex);
				}
			}, delegate(Row r, ItemGeneral b)
			{
				if (r.icon != null)
				{
					b.card = r.icon;
					b.button1.icon.transform.parent.SetActive(enable: true);
					r.icon.SetImage(b.button1.icon);
				}
				if ((bool)b.button1.keyText && index < Letters.Length)
				{
					b.button1.keyText.text = Letters[index].ToString();
				}
				index++;
			});
		}

		// Menu over inventory items matching a filter; optional extra rows on top
		// (e.g. "Drink from the well"). reopen=true rebuilds the menu after each
		// action so you can equip/drop several things in a row, like Elona.
		private static void ItemMenu(string title, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg, bool reopen = false, Func<Thing, string> label = null, List<Row> extraRows = null)
		{
			List<Thing> things = pc.things.List((Thing t) => Safe(filter, t), onlyAccessible: true);
			List<Row> rows = new List<Row>();
			if (extraRows != null)
			{
				rows.AddRange(extraRows);
			}
			foreach (Thing thing in things)
			{
				Thing t = thing;
				rows.Add(new Row
				{
					label = (label != null) ? label(t) : t.Name,
					icon = t,
					act = delegate
					{
						onSelect(t);
						if (reopen)
						{
							ItemMenu(title, filter, onSelect, emptyMsg, reopen, label);
						}
					}
				});
			}
			if (rows.Count == 0)
			{
				Msg.SayRaw(emptyMsg);
			}
			else
			{
				ShowMenu(title, rows);
			}
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

		private static ActPlan BuildPlan(Point p)
		{
			ActPlan plan = new ActPlan
			{
				input = ActInput.AllAction
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
		private static List<Row> TileActRows(string actId, string labelPrefix)
		{
			List<Row> rows = new List<Row>();
			foreach (Point p in Nearby())
			{
				foreach (ActPlan.Item i in BuildPlan(p).list)
				{
					if ((i.act as DynamicAct)?.id == actId)
					{
						ActPlan.Item item = i;
						rows.Add(new Row
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

		private static void RunRows(string title, List<Row> rows, string emptyMsg)
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
				ShowMenu(title, rows);
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
				reopen: false, label: null, extraRows: TileActRows("actDrink", "Drink: "));
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
				reopen: true);
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
					else if (!pc.TryEquip(t))
					{
						Msg.SayRaw("You cannot equip that.");
					}
				},
				"You have nothing to equip.",
				reopen: true,
				label: (Thing t) => t.isEquipped ? (t.Name + " <size=12>(equipped)</size>") : t.Name);
		}

		public static void Tool()
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

		// ---------- world keys ----------

		public static void Get()
		{
			List<Thing> loose = new List<Thing>();
			foreach (Thing t in pc.pos.Things)
			{
				if (!t.IsInstalled && t.trait.CanBeHeld)
				{
					loose.Add(t);
				}
			}
			if (loose.Count == 0)
			{
				Msg.SayRaw("There is nothing here to pick up.");
				return;
			}
			foreach (Thing t in loose)
			{
				pc.Pick(t);
			}
		}

		public static void Open()
		{
			List<Row> rows = new List<Row>();
			foreach (Point p in Nearby())
			{
				foreach (Thing t in p.Things)
				{
					if (!t.IsInstalled)
					{
						continue;
					}
					Thing target = t;
					if (t.trait.CanOpenContainer)
					{
						rows.Add(new Row
						{
							label = "Open: " + t.Name,
							icon = t,
							act = delegate
							{
								if (target.c_lockLv > 0)
								{
									pc.SetAIImmediate(new AI_OpenLock { target = target });
								}
								else
								{
									(target.trait as TraitContainer)?.TryOpen();
								}
							}
						});
					}
					else if (t.trait is TraitDoor door && !door.IsOpen())
					{
						TraitDoor d = door;
						rows.Add(new Row
						{
							label = "Open: " + t.Name,
							icon = t,
							act = delegate
							{
								if (d.owner.c_lockLv > 0)
								{
									pc.SetAIImmediate(new AI_OpenLock { target = d.owner.Thing });
								}
								else
								{
									pc.Say("openDoor", pc, d.owner);
									d.ToggleDoor();
									EClass.player.EndTurn();
								}
							}
						});
					}
				}
			}
			RunRows("Open what?", rows, "There is nothing to open nearby.");
		}

		public static void Close()
		{
			List<Row> rows = new List<Row>();
			foreach (Point p in Nearby())
			{
				foreach (Thing t in p.Things)
				{
					if (t.IsInstalled && t.trait is TraitDoor door && door.IsOpen() && door.CanClose())
					{
						TraitDoor d = door;
						rows.Add(new Row
						{
							label = "Close: " + t.Name,
							icon = t,
							act = delegate
							{
								pc.Say("close", pc, d.owner);
								d.ToggleDoor();
								EClass.player.EndTurn();
							}
						});
					}
				}
			}
			RunRows("Close what?", rows, "There is nothing to close nearby.");
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
			DirPrompt.Ask("Dig in which direction?", self: false, delegate(Point p)
			{
				if (!p.HasBlock || !TaskMine.CanMine(p, pc))
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
			List<Row> rows = new List<Row>();
			foreach (Point p in Nearby())
			{
				foreach (Thing t in p.Things)
				{
					if (t.IsInstalled && t.trait is TraitAltar altar)
					{
						TraitAltar a = altar;
						rows.Add(new Row
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
			List<Row> rows = new List<Row>();
			foreach (Point p in Nearby())
			{
				foreach (Chara c in p.Charas)
				{
					if (c != pc)
					{
						Chara target = c;
						rows.Add(new Row
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
