using System;
using System.Collections.Generic;

namespace ElonaKeys
{
	// Implementations of each classic Elona action on top of Elin's systems.
	public static class Handlers
	{
		private static Chara pc => EClass.pc;

		// ---------- helpers ----------

		private static void SelectItem(string prompt, Func<Thing, bool> filter, Action<Thing> onSelect, string emptyMsg)
		{
			List<Thing> matches = pc.things.List((Thing t) => Safe(filter, t), onlyAccessible: true);
			if (matches.Count == 0)
			{
				Msg.SayRaw(emptyMsg);
				return;
			}
			LayerDragGrid.Create(new InvOwnerSelectItem(prompt, filter, onSelect));
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

		// Enumerate the player's tile and the 8 neighbors.
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

		private static List<(Thing thing, T trait)> FindNearby<T>(Func<T, bool> valid = null) where T : Trait
		{
			List<(Thing, T)> found = new List<(Thing, T)>();
			foreach (Point p in Nearby())
			{
				foreach (Thing t in p.Things)
				{
					if (t.trait is T tr && (valid == null || valid(tr)))
					{
						found.Add((t, tr));
					}
				}
			}
			return found;
		}

		private static void PerformAt<T>(List<(Thing thing, T trait)> found, string emptyMsg, string prompt, Action<T> action) where T : Trait
		{
			if (found.Count == 0)
			{
				Msg.SayRaw(emptyMsg);
			}
			else if (found.Count == 1)
			{
				action(found[0].trait);
			}
			else
			{
				DirPrompt.Ask(prompt, self: true, delegate(Point p)
				{
					foreach ((Thing thing, T trait) pair in found)
					{
						if (pair.thing.pos.Equals(p))
						{
							action(pair.trait);
							return;
						}
					}
					Msg.SayRaw(emptyMsg);
				});
			}
		}

		// ---------- item actions (inventory selection) ----------

		public static void Eat()
		{
			SelectItem("Eat what?", (Thing t) => t.trait.CanEat(pc) && !t.isEquipped,
				delegate(Thing t) { t.DoAct(new AI_Eat { cook = false, target = t }); },
				"You have nothing to eat.");
		}

		public static void Quaff()
		{
			SelectItem("Quaff which potion?", (Thing t) => t.trait.CanDrink(pc),
				delegate(Thing t) { t.DoAct(new AI_Drink { target = t }); },
				"You have nothing to drink.");
		}

		public static void ReadItem()
		{
			SelectItem("Read what?", (Thing t) => t.trait.CanRead(pc),
				delegate(Thing t) { t.DoAct(new AI_Read { target = t }); },
				"You have nothing to read.");
		}

		public static void Zap()
		{
			SelectItem("Zap which rod?", (Thing t) => t.trait is TraitRod,
				delegate(Thing t)
				{
					DirPrompt.Ask("Zap in which direction? (press wait key for yourself)", self: true, delegate(Point p)
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
			SelectItem("Drop what?", (Thing t) => !t.isEquipped,
				delegate(Thing t) { pc.DropThing(t); },
				"You have nothing to drop.");
		}

		public static void Wear()
		{
			SelectItem("Wear or wield what?", (Thing t) => t.IsEquipmentOrRanged && !t.isEquipped,
				delegate(Thing t)
				{
					if (!pc.TryEquip(t))
					{
						Msg.SayRaw("You cannot equip that.");
					}
				},
				"You have nothing to equip.");
		}

		public static void Hold()
		{
			SelectItem("Hold what in your hands?", (Thing t) => t.trait.CanBeHeld && !t.isEquipped,
				delegate(Thing t) { pc.HoldCard(t); },
				"You have nothing you can hold.");
		}

		public static void Use()
		{
			SelectItem("Use what?", (Thing t) => t.trait.CanUse(pc),
				delegate(Thing t) { t.trait.OnUse(pc); },
				"You have nothing usable.");
		}

		public static void Throw()
		{
			SelectItem("Throw what?", (Thing t) => !t.isEquipped && t.trait.CanBeHeld,
				delegate(Thing t)
				{
					if (pc.enemy != null && pc.CanSee(pc.enemy) && !pc.enemy.isDead)
					{
						ActThrow.Throw(pc, pc.enemy.pos, t.Thing);
						EClass.player.EndTurn();
						return;
					}
					DirPrompt.Ask("Throw in which direction?", self: false, delegate(Point p)
					{
						ActThrow.Throw(pc, p, t.Thing);
						EClass.player.EndTurn();
					});
				},
				"You have nothing to throw.");
		}

		// ---------- world actions ----------

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
			var doors = FindNearby<TraitDoor>((TraitDoor d) => !d.IsOpen() && d.owner.IsInstalled);
			PerformAt(doors, "There is nothing to open nearby.", "Open which direction?", delegate(TraitDoor d)
			{
				if (d.owner.c_lockLv > 0)
				{
					pc.SetAIImmediate(new AI_OpenLock { target = d.owner.Thing });
					return;
				}
				pc.Say("openDoor", pc, d.owner);
				d.ToggleDoor();
				EClass.player.EndTurn();
			});
		}

		public static void Close()
		{
			var doors = FindNearby<TraitDoor>((TraitDoor d) => d.IsOpen() && d.CanClose() && d.owner.IsInstalled);
			PerformAt(doors, "There is nothing to close nearby.", "Close which direction?", delegate(TraitDoor d)
			{
				pc.Say("close", pc, d.owner);
				d.ToggleDoor();
				EClass.player.EndTurn();
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

		public static void Pray()
		{
			if (ActPray.TryPray(pc))
			{
				EClass.player.EndTurn();
			}
		}

		public static void Offer()
		{
			var altars = FindNearby<TraitAltar>();
			PerformAt(altars, "There is no altar nearby.", "Offer at which altar?", delegate(TraitAltar a)
			{
				LayerDragGrid.CreateOffering(a);
			});
		}

		public static void Interact()
		{
			DirPrompt.Ask("Interact with which direction? (press wait key for yourself)", self: true, delegate(Point p)
			{
				ShowActionsAt(p);
			});
		}

		private static void ShowActionsAt(Point p)
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
			if (!plan.HasAct)
			{
				Msg.SayRaw("There is nothing to interact with.");
			}
			else
			{
				plan.ShowContextMenu();
			}
		}

		public static void StairsDown()
		{
			UseStairs(down: true);
		}

		public static void StairsUp()
		{
			UseStairs(down: false);
		}

		private static void UseStairs(bool down)
		{
			if (EClass._zone.IsRegion)
			{
				if (down)
				{
					EClass.player.EnterLocalZone();
				}
				else
				{
					Msg.SayRaw("There are no stairs here.");
				}
				return;
			}
			TraitNewZone zone = down ? (TraitNewZone)pc.pos.FindThing<TraitStairsDown>() : pc.pos.FindThing<TraitStairsUp>();
			if (zone == null)
			{
				// teleporters, dungeon exits, zone gates and the like
				zone = pc.pos.FindThing<TraitNewZone>();
			}
			if (zone == null)
			{
				Msg.SayRaw(down ? "There are no stairs going down here." : "There are no stairs going up here.");
				return;
			}
			if ((EClass._zone.RegenerateOnEnter || EClass._zone.IsInstance) && !zone.CanUseInTempDungeon)
			{
				Msg.Say("badidea");
				return;
			}
			zone.MoveZone();
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
