using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElonaKeys
{
	// Implementations of each classic Elona action on top of Elin's systems.
	public static class Handlers
	{
		private static Chara pc => EClass.pc;

		private const string Letters = "abcdefghijklmnopqrstuvwxyz";
		private const float NameWidth = 470f;

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
			layer.SetSize(620f);
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
					b.button1.mainText.rectTransform.anchoredPosition = new Vector2(50f, 0f);
					Thing tip = r.icon;
					b.button1.SetTooltip("note", delegate(UITooltip tt)
					{
						tip.WriteNote(tt.note);
					});
				}
				if ((bool)b.button1.keyText && index < Letters.Length)
				{
					b.button1.keyText.text = Letters[index].ToString();
				}
				MenuExtras.ApplyNameDisplay(b.button1.mainText, NameWidth);
				index++;
			});
			MenuExtras.RegisterMenu(layer);
		}

		// Menu over inventory items matching a filter. reopen=true rebuilds the menu
		// after each action (equip/drop several in a row). When the list is large,
		// a category menu is shown first (configurable).
		private static void ItemMenu(string title, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg, bool reopen = false, Func<Thing, string> label = null, List<Row> extraRows = null, string category = null)
		{
			List<Thing> things = pc.things.List((Thing t) => Safe(filter, t), onlyAccessible: true);
			if (things.Count == 0 && (extraRows == null || extraRows.Count == 0))
			{
				Msg.SayRaw(emptyMsg);
				return;
			}
			if (category == null && Plugin.MenuCategories && things.Count > Plugin.CategoryThreshold)
			{
				CategoryMenu(title, things, filter, onSelect, emptyMsg, reopen, label, extraRows);
				return;
			}
			if (category != null)
			{
				things.RemoveAll((Thing t) => CategoryName(t) != category);
			}
			List<Row> rows = new List<Row>();
			if (extraRows != null && category == null)
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
							ItemMenu(title, filter, onSelect, emptyMsg, reopen, label, null, category);
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

		private static void CategoryMenu(string title, List<Thing> things, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg, bool reopen, Func<Thing, string> label, List<Row> extraRows)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			foreach (Thing t in things)
			{
				string c = CategoryName(t);
				counts.TryGetValue(c, out int n);
				counts[c] = n + 1;
			}
			List<Row> rows = new List<Row>();
			if (extraRows != null)
			{
				rows.AddRange(extraRows);
			}
			rows.Add(new Row
			{
				label = "All items (" + things.Count + ")",
				act = delegate { ItemMenu(title, filter, onSelect, emptyMsg, reopen, label, null, ""); }
			});
			foreach (KeyValuePair<string, int> kv in counts)
			{
				string cat = kv.Key;
				rows.Add(new Row
				{
					label = cat + " (" + kv.Value + ")",
					act = delegate { ItemMenu(title + " - " + cat, filter, onSelect, emptyMsg, reopen, label, null, cat); }
				});
			}
			ShowMenu(title, rows);
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
					else if (!pc.body.Equip(t))
					{
						Msg.SayRaw("You cannot equip that.");
					}
				},
				"You have nothing to equip.",
				reopen: true,
				label: (Thing t) => t.isEquipped ? (t.Name + " <size=12>(equipped)</size>") : t.Name);
		}

		public static void HoldTool()
		{
			ItemMenu("Hold which tool?", (Thing t) => t.trait is TraitTool && !t.isEquipped,
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

		// Classic 'o': containers and crafting kits, in inventory and nearby.
		public static void Open()
		{
			List<Row> rows = new List<Row>();
			foreach (Thing thing in pc.things.List((Thing t) => Safe((Thing x) => x.trait.CanOpenContainer || (x.trait is TraitCrafter && x.trait.CanUse(pc)), t), onlyAccessible: true))
			{
				Thing t = thing;
				rows.Add(new Row
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
						rows.Add(new Row
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
						rows.Add(new Row
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
