using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Keyboard navigation for Elin's NATIVE windows: context menus, dialogs,
	// drama talk choices, list windows, quest boards, crafting stations,
	// containers, quick-craft, ingredient dropdowns. Up/down move within the
	// current group, LEFT/RIGHT switch navigation groups (lists / item grids /
	// dropdowns / side buttons), z and x cycle the window's tabs (Elona style;
	// Tab stays native for chat), Enter activates (grids act like right-click,
	// dropdowns open their option list), x examines when an item grid is focused.
	public static class PopupNav
	{
		private class Group
		{
			public readonly List<UIButton> buttons = new List<UIButton>();
			public bool grid;
			public UIList list;        // for auto-scroll
			public UIDynamicList dyn;  // virtualized lists (crafting recipes): data navigation
			public readonly List<UIDropdown> drops = new List<UIDropdown>();
		}

		private static Object lastTarget;
		private static int groupIndex;
		private static int index = -1;
		private static readonly List<Group> groups = new List<Group>();

		// inventory/trade navigation is live (InputRouter fences arrows, z/x
		// and the native Tab inventory toggle while this is true)
		private static bool invNav;
		internal static bool InvNavActive => invNav;

		// the mod pause must not leave the movement/tab fences stuck on
		// (OnUpdate stops running while paused, so nothing else clears this)
		internal static void ClearInvNav()
		{
			invNav = false;
		}

		// "name  [8/40]" on inventory windows: used slots / capacity
		private static void ApplySlotCaption(LayerInventory li)
		{
			try
			{
				if (li.windows.Count == 0 || li.invs.Count == 0 || li.invs[0].owner == null)
				{
					return;
				}
				Card cont = li.invs[0].owner.Container;
				if (cont == null || cont.things == null)
				{
					return;
				}
				Window w = li.windows[0];
				string tag = "  [" + cont.things.Count + "/" + cont.things.MaxCapacity + "]";
				string cap = (w.textCaption != null) ? w.textCaption.text : null;
				if (cap != null && !cap.EndsWith(tag))
				{
					int cut = cap.LastIndexOf("  [");
					if (cut > 0 && cap.EndsWith("]"))
					{
						cap = cap.Substring(0, cut); // stale count from last refresh
					}
					w.SetCaption(cap + tag);
				}
			}
			catch
			{
				// cosmetic only
			}
		}

		// shift-release close tracking for native windows
		private static bool navShiftHeld;
		private static bool navShiftUsed;

		// debounced crafting selection: highlight moves instantly, the real
		// selection (full product-panel refresh + click sound) fires once the
		// cursor RESTS, so holding a key to scroll doesn't strobe the window
		private static UIButton pendingCraftBtn;
		private static float pendingCraftAt;

		private static void ApplyPendingCraftSelect()
		{
			if (pendingCraftBtn == null || !pendingCraftBtn)
			{
				pendingCraftBtn = null;
				return;
			}
			UIButton b = pendingCraftBtn;
			pendingCraftBtn = null;
			try
			{
				b.onClick.Invoke();
			}
			catch (System.Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys craft select failed: " + ex);
			}
		}

		public static void OnUpdate()
		{
			// a Dialog on top of an ElonaKeys menu (buy-amount popup over the
			// shop menu) is still ours to navigate; everything else defers
			// while a menu is open
			if (MenuSystem.Alive && !(EClass.ui != null && EClass.ui.TopLayer is Dialog))
			{
				return;
			}
			if (EInput.isInputFieldActive || EInput.isCtrlDown || EInput.isAltDown)
			{
				return;
			}
			// an open dropdown option list (ingredient/material pickers) owns the keys
			if ((bool)UIDropdown.activeInstance)
			{
				HandleDropdownPopup(UIDropdown.activeInstance);
				return;
			}
			groups.Clear();
			invNav = false;
			Object target = null;
			Layer topLayer = null;
			UIContextMenu ctx = UIContextMenu.Current;
			if ((bool)ctx)
			{
				target = ctx;
				Group g = new Group();
				CollectButtons(ctx.transform, g.buttons);
				if (g.buttons.Count > 0)
				{
					groups.Add(g);
				}
			}
			else
			{
				topLayer = (EClass.ui != null) ? EClass.ui.TopLayer : null;
				if (topLayer is LayerDrama)
				{
					// drama talk choices are loose buttons under DialogDrama.transChoices
					DialogDrama dd = topLayer.GetComponentInChildren<DialogDrama>(includeInactive: false);
					if (dd != null && dd.transChoices != null)
					{
						target = topLayer;
						Group g = new Group();
						CollectButtons(dd.transChoices, g.buttons);
						if (g.buttons.Count > 0)
						{
							groups.Add(g);
						}
					}
				}
				else if (topLayer is Dialog dialog && dialog.list != null)
				{
					target = dialog;
					Group g = new Group { list = dialog.list };
					CollectListButtons(dialog.list, g.buttons);
					if (g.buttons.Count > 0)
					{
						groups.Add(g);
					}
				}
				else if (topLayer is LayerList layerList && layerList.list != null)
				{
					target = layerList;
					Group g = new Group { list = layerList.list };
					CollectListButtons(layerList.list, g.buttons);
					if (g.buttons.Count > 0)
					{
						groups.Add(g);
					}
				}
				else if (topLayer is LayerInventory)
				{
					// TRADING / inventory windows: non-modal, so the generic
					// (BlockActions) branch never sees them. Every open
					// inventory window becomes a pane group: shop stock, your
					// backpack, open containers. Enter = the native right-click
					// (buy/sell/transfer with all its checks and dialogs),
					// Tab / left / right = hop panes, x = examine.
					target = topLayer;
					// scene-wide: ui.layers does NOT hold these (that was 1.25.0's
					// dead-on-arrival trading: zero panes found, zero errors)
					List<LayerInventory> lis = new List<LayerInventory>();
					foreach (LayerInventory li in UnityEngine.Object.FindObjectsOfType<LayerInventory>())
					{
						if (li != null && li.gameObject.activeInHierarchy)
						{
							lis.Add(li);
						}
					}
					// the window on top leads (a freshly opened shop = its stock)
					if (lis.Remove(topLayer as LayerInventory))
					{
						lis.Insert(0, topLayer as LayerInventory);
					}
					foreach (LayerInventory li in lis)
					{
						Group g = new Group { grid = true };
						foreach (ButtonGrid bg in li.GetComponentsInChildren<ButtonGrid>(includeInactive: false))
						{
							if (bg.card != null && bg.invOwner != null && bg.interactable && bg.gameObject.activeInHierarchy)
							{
								g.buttons.Add(bg);
							}
						}
						if (g.buttons.Count > 0)
						{
							SortByPosition(g.buttons);
							groups.Add(g);
						}
						ApplySlotCaption(li);
					}
					invNav = groups.Count > 0;
					if (Plugin.DebugLog && invNav)
					{
						Plugin.Log?.LogInfo("ElonaKeys trade nav: " + lis.Count + " inventory windows, " + groups.Count + " panes");
					}
				}
				else if (topLayer != null && (EClass.ui.BlockActions || topLayer is LayerCraftFloat))
				{
					// generic window: build all group kinds. LayerCraftFloat (quick
					// craft) is non-modal but still wants keyboard navigation.
					target = topLayer;
					// quest board: lists only (left/right = tabs, r = reroll).
					// crafting window: recipe list + material dropdowns only
					// (count/craft get direct keys; grids/loose buttons skipped)
					bool craftWindow = topLayer is LayerCraft;
					bool listsOnly = topLayer is LayerQuestBoard;
					// virtualized lists first (crafting recipes): navigate the DATA,
					// select-on-move gives the native click preview
					foreach (UIDynamicList dl in topLayer.GetComponentsInChildren<UIDynamicList>(includeInactive: false))
					{
						if (dl.objects != null && dl.objects.Count > 0)
						{
							groups.Add(new Group { dyn = dl });
						}
					}
					foreach (UIList ul in topLayer.GetComponentsInChildren<UIList>(includeInactive: false))
					{
						Group g = new Group { list = ul };
						CollectListButtons(ul, g.buttons);
						if (g.buttons.Count > 0)
						{
							groups.Add(g);
						}
					}
					if (craftWindow)
					{
						// keep material dropdowns, skip everything else
						Group craftDrops = new Group();
						foreach (UIDropdown dd in topLayer.GetComponentsInChildren<UIDropdown>(includeInactive: false))
						{
							if (dd.interactable && dd.gameObject.activeInHierarchy)
							{
								craftDrops.drops.Add(dd);
							}
						}
						if (craftDrops.drops.Count > 0)
						{
							groups.Add(craftDrops);
						}
						goto doneGroups;
					}
					if (listsOnly)
					{
						goto doneGroups;
					}
					Group grids = new Group { grid = true };
					foreach (ButtonGrid bg in topLayer.GetComponentsInChildren<ButtonGrid>(includeInactive: false))
					{
						if (bg.card != null && bg.invOwner != null && bg.interactable && bg.gameObject.activeInHierarchy && !InAnyGroup(bg))
						{
							grids.buttons.Add(bg);
						}
					}
					if (grids.buttons.Count > 0)
					{
						SortByPosition(grids.buttons);
						groups.Add(grids);
					}
					Group drops = new Group();
					foreach (UIDropdown dd in topLayer.GetComponentsInChildren<UIDropdown>(includeInactive: false))
					{
						if (dd.interactable && dd.gameObject.activeInHierarchy)
						{
							drops.drops.Add(dd);
						}
					}
					if (drops.drops.Count > 0)
					{
						groups.Add(drops);
					}
					Group loose = new Group();
					foreach (UIButton b in topLayer.GetComponentsInChildren<UIButton>(includeInactive: false))
					{
						// card-less ButtonGrids (e.g. crafting recipe rows) act like
						// plain buttons; card-carrying grids live in the grid group
						bool cardGrid = b is ButtonGrid bgb && bgb.card != null && bgb.invOwner != null;
						if (b.interactable && b.gameObject.activeInHierarchy && !cardGrid && !InAnyGroup(b))
						{
							loose.buttons.Add(b);
						}
					}
					if (loose.buttons.Count > 0)
					{
						SortByPosition(loose.buttons);
						groups.Add(loose);
					}
					doneGroups:;
				}
			}
			groups.RemoveAll((Group gr) => gr.dyn == null && gr.buttons.Count == 0 && gr.drops.Count == 0);
			// windows with tabs stay navigable (z/x) even with no button groups
			// (e.g. the character sheet)
			bool tabbable = topLayer != null && topLayer.windows != null && topLayer.windows.Count > 0
				&& (EClass.ui.BlockActions || topLayer is LayerCraftFloat);
			if (target == null && tabbable)
			{
				target = topLayer;
			}
			if (target == null || (groups.Count == 0 && !tabbable))
			{
				lastTarget = null;
				index = -1;
				groupIndex = 0;
				return;
			}
			if (target != lastTarget)
			{
				lastTarget = target;
				index = -1;
				groupIndex = 0;
			}
			groupIndex = Mathf.Clamp(groupIndex, 0, Mathf.Max(0, groups.Count - 1));
			HandleKeys(topLayer);
		}

		// keys for an OPEN dropdown option list: up/down apply the previous/next
		// option directly (the game refreshes live), Enter/Escape close it
		private static void HandleDropdownPopup(UIDropdown dd)
		{
			if (KeyRepeat.Down(KeyCode.DownArrow))
			{
				dd.Next();
			}
			else if (KeyRepeat.Down(KeyCode.UpArrow))
			{
				dd.Prev();
			}
			else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
			{
				dd.Hide();
				EInput.Consume();
			}
		}

		// reading order (top-to-bottom rows, left-to-right) so arrows follow the
		// visual layout instead of the hierarchy (chara sheet was chaotic)
		private static void SortByPosition(List<UIButton> buttons)
		{
			buttons.Sort(delegate(UIButton a, UIButton b)
			{
				Vector3 pa = a.transform.position;
				Vector3 pb = b.transform.position;
				if (Mathf.Abs(pa.y - pb.y) > 8f)
				{
					return pb.y.CompareTo(pa.y);
				}
				return pa.x.CompareTo(pb.x);
			});
		}

		private static bool InAnyGroup(UIButton b)
		{
			foreach (Group g in groups)
			{
				if (g.buttons.Contains(b))
				{
					return true;
				}
			}
			return false;
		}

		private static void CollectButtons(Transform root, List<UIButton> into)
		{
			foreach (UIButton b in root.GetComponentsInChildren<UIButton>(includeInactive: false))
			{
				if (b.interactable && b.gameObject.activeInHierarchy)
				{
					into.Add(b);
				}
			}
		}

		private static void CollectListButtons(UIList list, List<UIButton> into)
		{
			if (list.buttons == null)
			{
				return;
			}
			foreach (UIList.ButtonPair pair in list.buttons)
			{
				// rows come in three shapes: ItemGeneral (our menus), a plain
				// UIButton, or a composite holder with a button INSIDE it
				// (ItemQuest on the request boards — those rows were never
				// collected, which is why the boards would not scroll)
				UIButton b = (pair.component is ItemGeneral item) ? item.button1 : (pair.component as UIButton);
				if (b == null && pair.component != null)
				{
					b = pair.component.GetComponentInChildren<UIButton>(includeInactive: false);
				}
				if ((bool)b && b.interactable && b.gameObject.activeInHierarchy)
				{
					into.Add(b);
				}
			}
		}

		private static void HandleKeys(Layer topLayer)
		{
			// panes come and go between frames (windows close): never index
			// with a stale group cursor
			groupIndex = Mathf.Clamp(groupIndex, 0, Mathf.Max(0, groups.Count - 1));
			Group g = (groups.Count > 0) ? groups[groupIndex] : null;
			// Elona window family: Tab on the character sheet hops to the
			// equipment window (and back from there via MenuSystem's Tab)
			// trade/inventory panes: Tab hops buy <-> sell <-> containers,
			// Elona+CGX style (left/right do the same via the group switch)
			if (invNav && Input.GetKeyDown(KeyCode.Tab))
			{
				if (groups.Count > 1 && g != null)
				{
					Unhighlight(g);
					groupIndex = (groupIndex + 1) % groups.Count;
					index = 0;
					Highlight(groups[groupIndex]);
					EnsureVisible(groups[groupIndex]);
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.Tab) && topLayer is LayerChara && MenuCycle.Group == "chara" && !MenuCycle.CycledThisFrame)
			{
				// CycledThisFrame: MenuSystem's Tab already hopped Wear->Chara
				// THIS frame; reacting to the same press bounced it right back
				topLayer.Close();
				MenuCycle.Next();
				return;
			}
			// Shift closes native windows too (home/resident/request boards...),
			// same Elona muscle memory as the mod menus. Release-based so
			// shift+digit/shift+key combos stay untouched; talk scenes exempt.
			if (topLayer != null && !(topLayer is LayerDrama) && !EInput.isInputFieldActive)
			{
				if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
				{
					navShiftHeld = true;
					navShiftUsed = false;
				}
				else if (navShiftHeld && Input.anyKeyDown)
				{
					navShiftUsed = true;
				}
				if ((Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) && navShiftHeld)
				{
					navShiftHeld = false;
					if (!navShiftUsed)
					{
						topLayer.Close();
						return;
					}
				}
			}
			else
			{
				navShiftHeld = false;
			}
			// a dialog with nothing selected: Enter takes the DEFAULT (first)
			// button, so yes/no popups confirm without arrowing first
			if (topLayer is Dialog && g != null && g.buttons.Count > 0 && index < 0
				&& (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
			{
				g.buttons[0].onClick.Invoke();
				EInput.Consume();
				return;
			}
			// crafting window: +/- adjust the count (shift = 10s), Enter crafts
			// from the recipe list (dropdown group keeps Enter = open picker)
			if (topLayer is LayerCraft lc)
			{
				if (!EClass.pc.HasNoGoal)
				{
					pendingCraftBtn = null;
					return; // craft in progress: no re-fires; Escape cancels it
				}
				// apply the debounced selection once the cursor rests
				if (pendingCraftBtn != null && Time.unscaledTime >= pendingCraftAt)
				{
					ApplyPendingCraftSelect();
				}
				int step = EInput.isShiftDown ? 10 : 1;
				if (KeyRepeat.Down(KeyCode.Equals) || KeyRepeat.Down(KeyCode.KeypadPlus))
				{
					ApplyPendingCraftSelect(); // count applies to the shown recipe
					if (lc.recipe != null)
					{
						lc.inputNum.ModNum(step);
					}
					return;
				}
				if (KeyRepeat.Down(KeyCode.Minus) || KeyRepeat.Down(KeyCode.KeypadMinus))
				{
					ApplyPendingCraftSelect();
					if (lc.recipe != null)
					{
						lc.inputNum.ModNum(-step);
					}
					return;
				}
				// m = material picker, stateless: opens the first dropdown, and
				// with one already open, hops to the next slot. No group state
				// involved (the panel rebuilds destroy it under our feet).
				if (Input.GetKeyDown(KeyCode.M))
				{
					ApplyPendingCraftSelect();
					List<UIDropdown> dds = new List<UIDropdown>();
					foreach (UIDropdown dd in topLayer.GetComponentsInChildren<UIDropdown>(includeInactive: false))
					{
						if (dd.interactable && dd.gameObject.activeInHierarchy)
						{
							dds.Add(dd);
						}
					}
					if (dds.Count == 0)
					{
						SE.BeepSmall();
						return;
					}
					int open = (UIDropdown.activeInstance != null) ? dds.IndexOf(UIDropdown.activeInstance) : -1;
					if (open >= 0)
					{
						dds[open].Hide();
					}
					dds[(open + 1) % dds.Count].Show();
					return;
				}
				// Enter crafts ONLY from the recipe list (the dropdown group keeps
				// Enter = open picker; material access itself lives on m)
				bool onDrops = g != null && g.drops.Count > 0;
				if (!onDrops && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
				{
					ApplyPendingCraftSelect(); // craft what's under the cursor
					if (lc.recipe != null)
					{
						lc.OnClickCraft();
						EInput.Consume();
					}
					return;
				}
			}
			// quest board: left/right switch tabs, r hits the reroll button
			if (topLayer is LayerQuestBoard)
			{
				bool tabF = KeyRepeat.Down(KeyCode.RightArrow);
				bool tabB = KeyRepeat.Down(KeyCode.LeftArrow);
				if (tabF || tabB)
				{
					try
					{
						topLayer.windows[0].NextTab(tabF ? 1 : -1);
					}
					catch
					{
					}
					index = -1;
					return;
				}
				if (Input.GetKeyDown(KeyCode.R))
				{
					ClickMenuRight(topLayer);
					return;
				}
			}
			// quest boards: x = reroll (same as r; z alone covers the two tabs,
			// having BOTH z and x cycle them was redundant)
			if (topLayer is LayerQuestBoard && Input.GetKeyDown(KeyCode.X))
			{
				ClickMenuRight(topLayer);
				return;
			}
			// z / x cycle the window's tabs, Elona style (Tab stays native for
			// chat). x keeps its examine meaning while an item grid is focused.
			bool hasTabs = topLayer != null && topLayer.windows != null && topLayer.windows.Count > 0;
			bool gridFocused = g != null && g.grid && index >= 0 && index < g.buttons.Count;
			if (hasTabs && Input.GetKeyDown(KeyCode.Z))
			{
				try
				{
					topLayer.windows[0].NextTab(-1);
				}
				catch
				{
				}
				index = -1;
				return;
			}
			if (hasTabs && !gridFocused && Input.GetKeyDown(KeyCode.X))
			{
				try
				{
					topLayer.windows[0].NextTab(1);
				}
				catch
				{
				}
				index = -1;
				return;
			}
			if (g == null)
			{
				return; // tab-only window (character sheet)
			}
			bool next = KeyRepeat.Down(KeyCode.DownArrow);
			bool prev = KeyRepeat.Down(KeyCode.UpArrow);
			bool groupNext = KeyRepeat.Down(KeyCode.RightArrow);
			bool groupPrev = KeyRepeat.Down(KeyCode.LeftArrow);
			if (groups.Count > 1 && (groupNext || groupPrev))
			{
				Unhighlight(g);
				groupIndex = (groupIndex + (groupNext ? 1 : -1) + groups.Count) % groups.Count;
				index = 0;
				Highlight(groups[groupIndex]);
				EnsureVisible(groups[groupIndex]);
				return;
			}
			if (groups.Count == 1 && (groupNext || groupPrev))
			{
				next = groupNext; // single group: left/right also navigate
				prev = groupPrev;
			}
			// virtualized list group (crafting recipes): walk the DATA, selecting
			// each entry as we go (same as clicking it: product panel updates).
			// Position derives from the list's own selectedObject: the window
			// rebuilds freely without resetting our place.
			if (g.dyn != null)
			{
				if (next || prev)
				{
					int count = g.dyn.objects.Count;
					if (count == 0)
					{
						return;
					}
					int cur = g.dyn.objects.IndexOf(g.dyn.selectedObject);
					int ni;
					if (cur == -1)
					{
						ni = next ? 0 : (count - 1);
					}
					else
					{
						ni = (cur + (next ? 1 : -1) + count) % count;
					}
					object o = g.dyn.objects[ni];
					try
					{
						g.dyn.Scroll(Mathf.Max(0, g.dyn.GetIndex(o)));
						g.dyn.Select(o, invoke: true);
					}
					catch (System.Exception ex)
					{
						Plugin.Log?.LogError("ElonaKeys dyn-list nav failed: " + ex);
					}
				}
				return; // Enter not needed: selection already invoked
			}
			// dropdown group (ingredient/material pickers): up/down move between
			// dropdowns, Enter opens the option list (then arrows pick inside)
			if (g.drops.Count > 0)
			{
				g.drops.RemoveAll((UIDropdown d) => !d);
				if (g.drops.Count == 0)
				{
					return;
				}
				if (next || prev)
				{
					index = (Mathf.Max(index, 0) + (next ? 1 : -1) + g.drops.Count) % g.drops.Count;
					for (int i = 0; i < g.drops.Count; i++)
					{
						g.drops[i].transform.localScale = (i == index) ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
					}
					return;
				}
				if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && index >= 0 && index < g.drops.Count)
				{
					g.drops[index].Show();
					EInput.Consume();
				}
				return;
			}
			if (next || prev)
			{
				if (index == -1)
				{
					index = next ? 0 : (g.buttons.Count - 1);
				}
				else
				{
					index = (index + (next ? 1 : -1) + g.buttons.Count) % g.buttons.Count;
				}
				Highlight(g);
				EnsureVisible(g);
				// crafting recipe list: select the rested-on recipe (debounced;
				// the product panel refresh is too heavy to run per step)
				if (topLayer is LayerCraft && g.drops.Count == 0 && !g.grid && index >= 0 && index < g.buttons.Count)
				{
					pendingCraftBtn = g.buttons[index];
					pendingCraftAt = Time.unscaledTime + 0.15f;
				}
				return;
			}
			if (index < 0 || index >= g.buttons.Count)
			{
				return;
			}
			if (g.grid && Input.GetKeyDown(KeyCode.X))
			{
				if (g.buttons[index] is ButtonGrid bx && bx.card is Thing tx)
				{
					EClass.ui.AddLayerDontCloseOthers<LayerInfo>().SetThing(tx);
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				UIButton b = g.buttons[index];
				index = -1;
				lastTarget = null;
				if (g.grid && b is ButtonGrid bg)
				{
					bg.invOwner.OnRightClick(bg); // the transfer/use action
				}
				else
				{
					b.onClick.Invoke();
				}
				EInput.Consume();
			}
		}

		// first live button in the window's right-side menu (quest reroll)
		private static void ClickMenuRight(Layer topLayer)
		{
			try
			{
				WindowMenu mr = topLayer.windows[0].menuRight;
				if (mr == null || mr.layout == null)
				{
					return; // WindowMenu is a plain holder; its buttons live under layout
				}
				foreach (UIButton b in mr.layout.GetComponentsInChildren<UIButton>(includeInactive: false))
				{
					if (b.interactable && b.gameObject.activeInHierarchy)
					{
						b.onClick.Invoke();
						return;
					}
				}
			}
			catch
			{
			}
		}

		private static void EnsureVisible(Group g)
		{
			if (index < 0 || index >= g.buttons.Count)
			{
				return;
			}
			try
			{
				// scroll from the BUTTON itself, so every scrollable group
				// works (chara sheet skills/traits/strategy, not just UILists)
				UIButton b = g.buttons[index];
				if (!b)
				{
					return;
				}
				ScrollRect sr = b.GetComponentInParent<ScrollRect>();
				if (sr == null || !sr.vertical || sr.content == null)
				{
					return;
				}
				RectTransform vp = (sr.viewport != null) ? sr.viewport : sr.transform as RectTransform;
				float contentH = sr.content.rect.height;
				float viewH = vp.rect.height;
				if (contentH <= viewH + 1f)
				{
					return; // everything already fits
				}
				RectTransform bt = b.transform as RectTransform;
				float fromTop = Mathf.Abs(((Vector2)sr.content.InverseTransformPoint(bt.position)).y - sr.content.rect.yMax);
				// center the selection in the viewport
				sr.verticalNormalizedPosition = Mathf.Clamp01(1f - (fromTop - viewH * 0.5f) / (contentH - viewH));
			}
			catch
			{
				// cosmetic only
			}
		}

		private static void Highlight(Group g)
		{
			for (int i = 0; i < g.buttons.Count; i++)
			{
				if ((bool)g.buttons[i])
				{
					if (i == index)
					{
						g.buttons[i].DoHighlightTransition();
					}
					else
					{
						g.buttons[i].DoNormalTransition();
					}
				}
			}
		}

		private static void Unhighlight(Group g)
		{
			foreach (UIButton b in g.buttons)
			{
				if ((bool)b)
				{
					b.DoNormalTransition();
				}
			}
			foreach (UIDropdown d in g.drops)
			{
				if ((bool)d)
				{
					d.transform.localScale = Vector3.one;
				}
			}
		}
	}
}
