using System.Collections.Generic;
using UnityEngine;

namespace ElonaKeys
{
	// Keyboard navigation for Elin's NATIVE popups: right-click context menus,
	// yes/no dialogs, native list windows, and any window carrying an item grid
	// (containers, crafting stations). Arrows move a highlight, Enter clicks it
	// (on grids: the auto-transfer/use action, i.e. what right-click does),
	// x examines a grid item. Our own menus handle their own keys.
	public static class PopupNav
	{
		private static Object lastTarget;
		private static int index = -1;
		private static readonly List<UIButton> buttons = new List<UIButton>();
		private static bool gridMode;

		public static void OnUpdate()
		{
			if (MenuSystem.Alive || EInput.isInputFieldActive || EInput.isCtrlDown || EInput.isAltDown)
			{
				return;
			}
			Object target = null;
			buttons.Clear();
			gridMode = false;
			UIContextMenu ctx = UIContextMenu.Current;
			if ((bool)ctx)
			{
				target = ctx;
				CollectButtons(ctx.transform);
			}
			else
			{
				Layer top = (EClass.ui != null) ? EClass.ui.TopLayer : null;
				if (top is Dialog dialog && dialog.list != null)
				{
					target = dialog;
					CollectListButtons(dialog.list);
				}
				else if (top is LayerList layerList && layerList.list != null)
				{
					target = layerList;
					CollectListButtons(layerList.list);
				}
				else if (top != null)
				{
					// generic: item grids first (containers, crafting), then any list
					foreach (ButtonGrid bg in top.GetComponentsInChildren<ButtonGrid>(includeInactive: false))
					{
						if (bg.card != null && bg.invOwner != null && bg.interactable && bg.gameObject.activeInHierarchy)
						{
							buttons.Add(bg);
						}
					}
					if (buttons.Count > 0)
					{
						target = top;
						gridMode = true;
					}
					else
					{
						UIList ul = top.GetComponentInChildren<UIList>(includeInactive: false);
						if (ul != null)
						{
							target = top;
							CollectListButtons(ul);
						}
					}
				}
			}
			if (target == null || buttons.Count == 0)
			{
				lastTarget = null;
				index = -1;
				return;
			}
			if (target != lastTarget)
			{
				lastTarget = target;
				index = -1;
			}
			HandleKeys();
		}

		private static void CollectButtons(Transform root)
		{
			foreach (UIButton b in root.GetComponentsInChildren<UIButton>(includeInactive: false))
			{
				if (b.interactable && b.gameObject.activeInHierarchy)
				{
					buttons.Add(b);
				}
			}
		}

		private static void CollectListButtons(UIList list)
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
					buttons.Add(b);
				}
			}
		}

		private static void HandleKeys()
		{
			bool next = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow);
			bool prev = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow);
			if (next || prev)
			{
				if (index == -1)
				{
					index = next ? 0 : (buttons.Count - 1);
				}
				else
				{
					index = (index + (next ? 1 : -1) + buttons.Count) % buttons.Count;
				}
				Highlight();
				return;
			}
			if (index < 0 || index >= buttons.Count)
			{
				return;
			}
			if (gridMode && Input.GetKeyDown(KeyCode.X))
			{
				if (buttons[index] is ButtonGrid bx && bx.card is Thing tx)
				{
					EClass.ui.AddLayerDontCloseOthers<LayerInfo>().SetThing(tx);
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				UIButton b = buttons[index];
				index = -1;
				lastTarget = null;
				if (gridMode && b is ButtonGrid bg)
				{
					// same as right-click: auto-transfer/use through the game's logic
					bg.invOwner.OnRightClick(bg);
				}
				else
				{
					b.onClick.Invoke();
				}
				EInput.Consume();
			}
		}

		private static void Highlight()
		{
			for (int i = 0; i < buttons.Count; i++)
			{
				if ((bool)buttons[i])
				{
					if (i == index)
					{
						buttons[i].DoHighlightTransition();
					}
					else
					{
						buttons[i].DoNormalTransition();
					}
				}
			}
		}
	}
}
