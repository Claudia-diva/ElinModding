using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Keyboard navigation for Elin's NATIVE windows: context menus, dialogs,
	// drama talk choices, list windows, quest boards, crafting stations,
	// containers. Up/down move within the current group, LEFT/RIGHT switch
	// navigation groups (lists / item grids / side buttons), TAB cycles the
	// window's tabs, Enter activates (grids act like right-click), x examines.
	public static class PopupNav
	{
		private class Group
		{
			public readonly List<UIButton> buttons = new List<UIButton>();
			public bool grid;
			public UIList list; // for auto-scroll
		}

		private static Object lastTarget;
		private static int groupIndex;
		private static int index = -1;
		private static readonly List<Group> groups = new List<Group>();

		public static void OnUpdate()
		{
			if (MenuSystem.Alive || EInput.isInputFieldActive || EInput.isCtrlDown || EInput.isAltDown)
			{
				return;
			}
			groups.Clear();
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
				else if (topLayer != null && EClass.ui.BlockActions)
				{
					// generic modal window: build all three group kinds
					target = topLayer;
					foreach (UIList ul in topLayer.GetComponentsInChildren<UIList>(includeInactive: false))
					{
						Group g = new Group { list = ul };
						CollectListButtons(ul, g.buttons);
						if (g.buttons.Count > 0)
						{
							groups.Add(g);
						}
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
						groups.Add(grids);
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
						groups.Add(loose);
					}
				}
			}
			if (target == null || groups.Count == 0)
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
			groupIndex = Mathf.Clamp(groupIndex, 0, groups.Count - 1);
			HandleKeys(topLayer);
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
				UIButton b = (pair.component is ItemGeneral item) ? item.button1 : (pair.component as UIButton);
				if ((bool)b && b.interactable && b.gameObject.activeInHierarchy)
				{
					into.Add(b);
				}
			}
		}

		private static void HandleKeys(Layer topLayer)
		{
			Group g = groups[groupIndex];
			// Tab cycles the window's tabs (quest board, crafting categories)
			if (Input.GetKeyDown(KeyCode.Tab) && topLayer != null && topLayer.windows != null && topLayer.windows.Count > 0)
			{
				topLayer.windows[0].NextTab(-1);
				index = -1;
				return;
			}
			bool next = Input.GetKeyDown(KeyCode.DownArrow);
			bool prev = Input.GetKeyDown(KeyCode.UpArrow);
			bool groupNext = Input.GetKeyDown(KeyCode.RightArrow);
			bool groupPrev = Input.GetKeyDown(KeyCode.LeftArrow);
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

		private static void EnsureVisible(Group g)
		{
			if (index < 0 || index >= g.buttons.Count)
			{
				return;
			}
			try
			{
				if (g.list != null && g.buttons.Count > 1)
				{
					ScrollRect sr = g.list.GetComponentInParent<ScrollRect>();
					if (sr != null && sr.vertical)
					{
						sr.verticalNormalizedPosition = Mathf.Clamp01(1f - (float)index / (g.buttons.Count - 1));
					}
				}
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
		}
	}
}
