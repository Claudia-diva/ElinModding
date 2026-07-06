using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ElonaKeys
{
	// Elona-style crafting: a recipe LIST (craftable first, skill + max count),
	// Enter opens the recipe: ingredient rows cycle through every owned material
	// (inventory + home stock via the game's own search), a Craft row with a
	// count spinner executes through TaskCraft (the same headless task the
	// quick-craft float uses; the native LayerCraft window stays reachable).
	internal static class CraftMenu
	{
		internal static bool bypass; // "(open the native window...)" escape hatch

		private static string craftCat = "all";

		public static void Show(Thing factory)
		{
			craftCat = "all";
			ShowCat(factory);
		}

		// second-root category of a recipe, collapsed like the native tabs
		private static string CatOf(RecipeSource rs)
		{
			try
			{
				SourceCategory.Row root = rs.row.Category.GetSecondRoot();
				if (root.id != "lightsource" && root.IsChildOf("armor"))
				{
					root = EClass.sources.categories.map["armor"];
				}
				return root.id;
			}
			catch
			{
				return "other";
			}
		}

		private static string CatName(string id)
		{
			if (id == "all")
			{
				return "All";
			}
			try
			{
				return EClass.sources.categories.map[id].GetName();
			}
			catch
			{
				return id;
			}
		}

		private static void ShowCat(Thing factory)
		{
			List<RecipeSource> sources;
			try
			{
				sources = EClass.player.recipes.ListSources(factory, null);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys craft list failed: " + ex);
				OpenNative(factory);
				return;
			}
			// category tabs, native style: All + each present second-root category
			List<string> cats = new List<string> { "all" };
			foreach (RecipeSource rs0 in sources)
			{
				string c = CatOf(rs0);
				if (!cats.Contains(c))
				{
					cats.Add(c);
				}
			}
			if (!cats.Contains(craftCat))
			{
				craftCat = "all";
			}
			List<MenuRow> can = new List<MenuRow>();
			List<MenuRow> cant = new List<MenuRow>();
			foreach (RecipeSource rs in sources)
			{
				if (craftCat != "all" && CatOf(rs) != craftCat)
				{
					continue;
				}
				Recipe r;
				try
				{
					r = Recipe.Create(rs);
					r.BuildIngredientList();
					AutoAssign(r);
				}
				catch
				{
					continue;
				}
				bool craftable = SafeCraftable(r);
				int max = craftable ? SafeMaxCount(r) : 0;
				string skill = "";
				try
				{
					Element sk = rs.GetReqSkill();
					skill = sk.Name + " " + sk.Value;
				}
				catch
				{
				}
				Recipe rr = r;
				Thing fac = factory;
				(craftable ? can : cant).Add(new MenuRow
				{
					label = craftable ? SafeName(rr) : ("<color=#777777>" + SafeName(rr) + "</color>"),
					rightText = "<size=12>" + skill + (craftable ? ("  x" + max) : "") + "</size>",
					closeOnAct = false,
					act = delegate { Detail(rr, fac); }
				});
			}
			List<MenuRow> rows = new List<MenuRow>();
			rows.AddRange(can);
			rows.AddRange(cant);
			rows.Add(new MenuRow
			{
				label = "(open the native window...)",
				act = delegate { OpenNative(factory); }
			});
			string title = ((factory != null) ? (factory.Name + " - ") : "") + "Craft [" + CatName(craftCat) + "]";
			MenuSystem.Show(title, rows);
			// z/x cycle the category tabs, same keys as everywhere else
			List<string> catList = cats;
			Thing fac2 = factory;
			MenuSystem.SetSourceCycle(delegate(int dir)
			{
				int cur = Mathf.Max(0, catList.IndexOf(craftCat));
				craftCat = catList[((cur + dir) % catList.Count + catList.Count) % catList.Count];
				ShowCat(fac2);
			});
		}

		private static string SafeName(Recipe r)
		{
			try
			{
				return r.Name;
			}
			catch
			{
				return r.id;
			}
		}

		private static bool SafeCraftable(Recipe r)
		{
			try
			{
				return r.IsCraftable();
			}
			catch
			{
				return false;
			}
		}

		private static int SafeMaxCount(Recipe r)
		{
			try
			{
				return Mathf.Max(0, r.GetMaxCount());
			}
			catch
			{
				return 0;
			}
		}

		// first candidate with enough quantity per non-optional ingredient;
		// the game's search spans inventory + home stock (StockSearchMode.AroundPC)
		private static void AutoAssign(Recipe r)
		{
			foreach (Recipe.Ingredient ing in r.ingredients)
			{
				if (ing.optional || (ing.thing != null && !ing.thing.isDestroyed && ing.thing.Num >= ing.req))
				{
					continue;
				}
				ThingStack st = EClass._map.Stocked.ListThingStack(ing, StockSearchMode.AroundPC);
				foreach (Thing t in st.list)
				{
					if (t.Num >= ing.req)
					{
						ing.SetThing(t);
						break;
					}
				}
			}
		}

		private static void Detail(Recipe r, Thing factory)
		{
			MenuSystem.Show(SafeName(r), BuildDetailRows(r, factory), delegate
			{
				MenuSystem.SetRows(BuildDetailRows(r, factory));
			});
		}

		private static List<MenuRow> BuildDetailRows(Recipe r, Thing factory)
		{
			List<MenuRow> rows = new List<MenuRow>();
			foreach (Recipe.Ingredient ingredient in r.ingredients)
			{
				Recipe.Ingredient ing = ingredient;
				string ingName;
				try
				{
					ingName = ing.GetName();
				}
				catch
				{
					ingName = ing.id;
				}
				string assigned = (ing.thing != null && !ing.thing.isDestroyed)
					? (ing.thing.Name + " <color=#8f8f7a>(have " + ing.thing.Num + ")</color>")
					: "<color=#a07070>none</color>";
				rows.Add(new MenuRow
				{
					fixedPrefix = "x" + ing.req + ":",
					label = ingName + " → " + assigned + (ing.optional ? " <size=12>(optional)</size>" : ""),
					icon = (ing.thing != null && !ing.thing.isDestroyed) ? ing.thing : null,
					closeOnAct = false,
					act = delegate { CycleIngredient(r, ing); }
				});
			}
			int max = SafeMaxCount(r);
			rows.Add(new MenuRow
			{
				label = (max > 0) ? ("Craft  <color=#9fd6ff>(up to x" + max + ")</color>") : "<color=#777777>Craft (missing materials)</color>",
				closeOnAct = false,
				maxAmount = Mathf.Max(0, max),
				onAmount = delegate(int n) { Execute(r, factory, n); },
				act = (max == 1) ? ((Action)delegate { Execute(r, factory, 1); }) : null
			});
			return rows;
		}

		// Enter on an ingredient row: rotate to the next owned candidate
		private static void CycleIngredient(Recipe r, Recipe.Ingredient ing)
		{
			try
			{
				ThingStack st = EClass._map.Stocked.ListThingStack(ing, StockSearchMode.AroundPC);
				if (st.list.Count == 0)
				{
					Msg.SayRaw("You own nothing that fits.");
					return;
				}
				int cur = (ing.thing != null) ? st.list.IndexOf(ing.thing) : -1;
				for (int step = 1; step <= st.list.Count; step++)
				{
					Thing t = st.list[(cur + step) % st.list.Count];
					if (t.Num >= ing.req || st.list.Count == 1)
					{
						ing.SetThing(t);
						break;
					}
				}
				try
				{
					r.SaveLastIngredients();
				}
				catch
				{
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys ingredient cycle failed: " + ex);
			}
		}

		// verbatim TaskCraft init from LayerCraftFloat.cs:91-106
		private static void Execute(Recipe r, Thing factory, int num)
		{
			AutoAssign(r); // things may have died since the menu opened
			if (!SafeCraftable(r) || SafeMaxCount(r) < num)
			{
				Msg.SayRaw("You no longer have the materials.");
				return;
			}
			try
			{
				TaskCraft taskCraft = new TaskCraft
				{
					recipe = r,
					num = num,
					repeat = false,
					floatMode = true
				};
				if (factory != null)
				{
					taskCraft.factory = factory;
				}
				taskCraft.ResetReq();
				if (!taskCraft.IsIngredientsValid(destoryResources: false, taskCraft.num))
				{
					SE.Beep();
					Msg.SayRaw("The materials don't check out.");
					return;
				}
				MenuSystem.Close();
				EClass.pc.SetAI(taskCraft);
				ActionMode.Adv.SetTurbo();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys craft failed: " + ex);
				Msg.SayRaw("The craft failed to start (see log).");
			}
		}

		internal static void OpenNative(Thing factory)
		{
			bypass = true;
			try
			{
				MenuSystem.Close();
				EClass.ui.AddLayer<LayerCraft>().SetFactory(factory);
			}
			finally
			{
				bypass = false;
			}
		}
	}

	// Every crafting entry point (stations, the quick-craft action) funnels
	// through LayerCraft.SetFactory: redirect to the keyboard menu.
	[HarmonyPatch(typeof(LayerCraft), nameof(LayerCraft.SetFactory))]
	internal static class Patch_CraftOpen
	{
		private static bool Prefix(LayerCraft __instance, Thing t)
		{
			if (!Plugin.KeyboardCrafting || CraftMenu.bypass)
			{
				return true;
			}
			try
			{
				__instance.Close();
				CraftMenu.Show(t);
				return false;
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys craft redirect failed: " + ex);
				return true;
			}
		}
	}
}
