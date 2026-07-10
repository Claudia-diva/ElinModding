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
			ShowCat(factory, replaceScreen: false);
		}

		private static void ShowCat(Thing factory, bool replaceScreen)
		{
			// throws propagate to the redirect postfix, which keeps the native
			// window when our menu can't open (never open a SECOND native one)
			List<RecipeSource> sources = EClass.player.recipes.ListSources(factory, null);
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
				int own = OwnedCount(r);
				Recipe rr = r;
				Thing fac = factory;
				// native-style coloring: craftable green, uncraftable gray;
				// labeled counts instead of naked numbers
				string tag = " <size=13><color=#8f8f7a>" + skill
					+ ((own > 0) ? ("  own " + own) : "")
					+ (craftable ? ("  make x" + max) : "  missing materials") + "</color></size>";
				string name = craftable
					? ("<color=#3a7d3a>" + SafeName(rr) + "</color>")
					: ("<color=#8a8a8a>" + SafeName(rr) + "</color>");
				(craftable ? can : cant).Add(new MenuRow
				{
					label = name + tag,
					closeOnAct = false,
					drawIcon = delegate(UnityEngine.UI.Image img) { rr.SetImage(img); },
					note = delegate(UINote n) { WriteRecipeNote(n, rr, fac); },
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
			MenuSystem.Show(title, rows, null, keepLayers: false, replace: replaceScreen);
			// z/x cycle the category tabs; holding x resets to [All]; cycling
			// REPLACES the screen so shift exits instead of replaying every tab
			List<string> catList = cats;
			Thing fac2 = factory;
			MenuSystem.SetSourceCycle(delegate(int dir)
			{
				if (dir == 0)
				{
					craftCat = "all";
				}
				else
				{
					int cur = Mathf.Max(0, catList.IndexOf(craftCat));
					craftCat = catList[((cur + dir) % catList.Count + catList.Count) % catList.Count];
				}
				ShowCat(fac2, replaceScreen: true);
			});
		}

		// how many of the product the player already owns (native "Own" column)
		private static int OwnedCount(Recipe r)
		{
			try
			{
				string id = r.id; // recipe id == product card id
				int n = 0;
				foreach (Thing t in EClass.pc.things.List((Thing x) => x.id == id, onlyAccessible: true))
				{
					n += t.Num;
				}
				return n;
			}
			catch
			{
				return 0;
			}
		}

		private static int SpCost(Recipe r, Thing factory)
		{
			try
			{
				return r.source.GetSPCost(factory);
			}
			catch
			{
				return -1;
			}
		}

		// tooltip / detail summary matching what the native product panel shows
		private static void WriteRecipeNote(UINote n, Recipe r, Thing factory)
		{
			n.AddHeader(SafeName(r));
			try
			{
				Element sk = r.source.GetReqSkill();
				int cur = EClass.pc.Evalue(sk.id);
				n.AddText("Skill: " + sk.Name + " " + sk.Value + "  (you: " + cur + ")");
			}
			catch
			{
			}
			int sp = SpCost(r, factory);
			if (sp >= 0)
			{
				bool hurts = sp >= EClass.pc.stamina.value;
				n.AddText("Stamina: " + sp + " per craft  (you: " + EClass.pc.stamina.value + ")" + (hurts ? " It hurts!" : ""));
			}
			int max = SafeMaxCount(r);
			n.AddText("Craftable now: " + ((max > 0) ? ("x" + max) : "none"));
			int own = OwnedCount(r);
			if (own > 0)
			{
				n.AddText("Owned: " + own);
			}
			n.Space(4);
			foreach (Recipe.Ingredient ing in r.ingredients)
			{
				string nm;
				try
				{
					nm = ing.GetName();
				}
				catch
				{
					nm = ing.id;
				}
				int have = 0;
				try
				{
					have = EClass._map.Stocked.ListThingStack(ing, StockSearchMode.AroundPC).count;
				}
				catch
				{
				}
				n.AddText("- " + nm + " x" + ing.req + "  (have " + have + ")" + (ing.optional ? " (optional)" : ""));
			}
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
			// info header, like the native product panel
			string info = "";
			try
			{
				Element sk = r.source.GetReqSkill();
				info = sk.Name + " " + sk.Value + " (you " + EClass.pc.Evalue(sk.id) + ")";
			}
			catch
			{
			}
			int sp = SpCost(r, factory);
			if (sp >= 0)
			{
				bool hurts = sp >= EClass.pc.stamina.value;
				info += "   Stamina " + (hurts ? ("<color=#c05050>" + sp + "!</color>") : sp.ToString()) + "/" + EClass.pc.stamina.value;
			}
			int owned = OwnedCount(r);
			if (owned > 0)
			{
				info += "   own " + owned;
			}
			if (info.Length > 0)
			{
				rows.Add(new MenuRow
				{
					label = "<size=13>" + info.Trim() + "</size>",
					note = delegate(UINote n) { WriteRecipeNote(n, r, factory); }
				});
			}
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
					act = delegate { PickIngredient(r, ing); }
				});
			}
			int max = SafeMaxCount(r);
			rows.Add(new MenuRow
			{
				label = (max > 0) ? ("Craft  <color=#9fd6ff>(up to x" + max + ")</color>") : "<color=#777777>Craft (missing materials)</color>",
				closeOnAct = false,
				maxAmount = Mathf.Max(0, max),
				note = delegate(UINote n) { WriteRecipeNote(n, r, factory); },
				onAmount = delegate(int n) { Execute(r, factory, n); },
				act = (max == 1) ? ((Action)delegate { Execute(r, factory, 1); }) : null
			});
			return rows;
		}

		// Enter on an ingredient row: PICK from a list of every owned candidate
		// (sprites, counts, current selection marked); picking returns to the
		// recipe with the material applied and remembered
		private static void PickIngredient(Recipe r, Recipe.Ingredient ing)
		{
			try
			{
				ThingStack st = EClass._map.Stocked.ListThingStack(ing, StockSearchMode.AroundPC);
				if (st.list.Count == 0)
				{
					Msg.SayRaw(EKLang.T("You own nothing that fits."));
					return;
				}
				List<MenuRow> rows = new List<MenuRow>();
				foreach (Thing thing in st.list)
				{
					Thing t = thing;
					bool selected = ing.thing == t;
					bool enough = t.Num >= ing.req;
					rows.Add(new MenuRow
					{
						label = t.Name
							+ (selected ? " <color=#3a7d3a>(selected)</color>" : "")
							+ (enough ? "" : " <color=#8a8a8a>(not enough)</color>"),
						icon = t,
						sortWeight = t.ChildrenAndSelfWeight,
						act = delegate
						{
							ing.SetThing(t);
							try
							{
								r.SaveLastIngredients();
							}
							catch
							{
							}
							SE.Equip();
						},
						closeOnAct = false
					});
				}
				string ingName;
				try
				{
					ingName = ing.GetName();
				}
				catch
				{
					ingName = ing.id;
				}
				// wrap each act so picking pops back to the recipe view refreshed
				foreach (MenuRow row in rows)
				{
					Action inner = row.act;
					row.act = delegate
					{
						inner();
						MenuSystem.BackOne();
						MenuSystem.RefreshCurrent();
					};
				}
				MenuSystem.Show("Pick " + ingName + " (need x" + ing.req + ")", rows);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys ingredient pick failed: " + ex);
			}
		}

		// verbatim TaskCraft init from LayerCraftFloat.cs:91-106
		private static void Execute(Recipe r, Thing factory, int num)
		{
			AutoAssign(r); // things may have died since the menu opened
			if (!SafeCraftable(r) || SafeMaxCount(r) < num)
			{
				Msg.SayRaw(EKLang.T("You no longer have the materials."));
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
					Msg.SayRaw(EKLang.T("The materials don't check out."));
					return;
				}
				MenuSystem.Close();
				EClass.pc.SetAI(taskCraft);
				ActionMode.Adv.SetTurbo();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys craft failed: " + ex);
				Msg.SayRaw(EKLang.T("The craft failed to start (see log)."));
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
	// POSTFIX on purpose: closing the layer before SetFactory initializes it
	// tears down a half-built window (NRE mid-Close, layer stuck un-closable).
	// After SetFactory the window is in its normal closable state.
	[HarmonyPatch(typeof(LayerCraft), nameof(LayerCraft.SetFactory))]
	internal static class Patch_CraftOpen
	{
		private static void Postfix(LayerCraft __instance, Thing t)
		{
			if (InputRouter.VanillaLocked || Plugin.CraftingStyle != "menu" || CraftMenu.bypass)
			{
				return; // default (or mod paused): the native window stays
			}
			bool ours = false;
			try
			{
				CraftMenu.Show(t);
				ours = true;
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys craft redirect failed (native window kept): " + ex);
			}
			if (ours)
			{
				try
				{
					if (__instance != null && (bool)__instance)
					{
						__instance.Close();
					}
				}
				catch (Exception ex2)
				{
					Plugin.Log?.LogError("ElonaKeys native craft close failed: " + ex2);
				}
			}
		}
	}
}
