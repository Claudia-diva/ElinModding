using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// The Elona-style menu window: paginated, fully keyboard-navigable.
	// - fixed rows per page, left/right (or PageUp/Down) switch pages
	// - up/down move the ► cursor, Enter/letter/click act
	// - weight column on the right, z cycles sort (original/name/weight)
	// - Shift goes back to the previous window (or closes), like Elona
	// - stack rows open a « n (max) » count spinner: left/right ±1, up/down ±10
	public class MenuRow
	{
		public string label;
		public Thing icon;
		public Action act;
		public bool closeOnAct = true;
		public string rightText;       // usually the weight column
		public int sortWeight;         // for the weight sort mode
		public int maxAmount;          // >1 enables the count spinner
		public Action<int> onAmount;   // called with the chosen count
	}

	public static class MenuSystem
	{
		private const string Letters = "abcdefghijklmnopqrstuvwxyz";

		private class Screen
		{
			public string title;
			public List<MenuRow> rows;
			public Action refreshSource;
		}

		private static LayerList layer;
		private static Screen current;
		private static readonly List<Screen> history = new List<Screen>();
		private static readonly List<MenuRow> pageRows = new List<MenuRow>();
		private static List<MenuRow> view = new List<MenuRow>();
		private static int page;
		private static int sel;
		private static int sortMode; // 0 original, 1 name, 2 weight
		private static int openFrame;

		// count spinner state
		private static MenuRow amountRow;
		private static int amount;

		// shift = back, but only if shift wasn't a modifier for something else
		private static bool shiftHeld;
		private static bool shiftUsed;

		public static bool Alive => layer != null && (bool)layer;

		// max 20 rows so x (examine) and z (sort) stay free as letters
		private static int PageSize => Mathf.Clamp(Plugin.MenuPageSize, 5, 20);

		private static int PageCount => Mathf.Max(1, (view.Count + PageSize - 1) / PageSize);

		public static void Show(string _title, List<MenuRow> rows, Action _refreshSource = null)
		{
			if (Alive && current != null)
			{
				history.Add(current); // shift returns here
			}
			else
			{
				history.Clear();
			}
			current = new Screen { title = _title, rows = rows, refreshSource = _refreshSource };
			page = 0;
			sel = 0;
			amountRow = null;
			openFrame = Time.frameCount;
			if (!Alive)
			{
				layer = EClass.ui.AddLayer<LayerList>();
				layer.SetSize(680f);
				layer.SetList2(pageRows, RowText, delegate(MenuRow r, ItemGeneral b)
				{
					Activate(r);
				}, BuildRow, autoClose: false);
			}
			ApplyPage();
			MenuExtras.RegisterMenu(layer);
		}

		public static void Close()
		{
			history.Clear();
			current = null;
			amountRow = null;
			if (Alive)
			{
				layer.Close();
			}
			layer = null;
		}

		private static void Back()
		{
			if (amountRow != null)
			{
				amountRow = null;
				ApplyPage();
				return;
			}
			if (history.Count == 0)
			{
				Close();
				return;
			}
			Screen prev = history[history.Count - 1];
			history.RemoveAt(history.Count - 1);
			current = prev;
			page = 0;
			sel = 0;
			amountRow = null;
			prev.refreshSource?.Invoke();
			ApplyPage();
		}

		public static void SetRows(List<MenuRow> rows)
		{
			if (current != null)
			{
				current.rows = rows;
			}
		}

		private static string RowText(MenuRow r)
		{
			int i = pageRows.IndexOf(r);
			string cursor = (i == sel) ? "► " : "    ";
			string letter = (i >= 0 && i < Letters.Length) ? (Letters[i] + ")  ") : "";
			string spin = (r == amountRow) ? ("   « " + amount + " (" + r.maxAmount + ") »") : "";
			// weight lives inside the label: the list mold has no right-column slot
			string weight = string.IsNullOrEmpty(r.rightText) ? "" : ("   <size=13><color=#8f8f7a>" + r.rightText + "</color></size>");
			return cursor + letter + r.label + spin + weight;
		}

		private static void BuildRow(MenuRow r, ItemGeneral b)
		{
			// cosmetic failures must never abort UIList.List(): a thrown
			// onInstantiate leaves half-built windows and stale pooled rows
			try
			{
				BuildRowInner(r, b);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys row build failed: " + ex);
			}
		}

		private static void BuildRowInner(MenuRow r, ItemGeneral b)
		{
			if ((bool)b.button1.keyText)
			{
				b.button1.keyText.text = "";
				b.button1.keyText.SetActive(enable: false);
			}
			if (r.icon != null)
			{
				b.card = r.icon;
				RectTransform iconParent = b.button1.icon.transform.parent as RectTransform;
				iconParent.SetActive(enable: true);
				iconParent.anchoredPosition = new Vector2(24f, 0f);
				Image img = b.button1.icon;
				r.icon.SetImage(img);
				// RenderRow.SetImage assumes the inventory-grid material and render
				// pivots; normalize so the sprite shows inside the small slot.
				img.color = Color.white;
				RectTransform rt = img.rectTransform;
				rt.pivot = new Vector2(0.5f, 0.5f);
				rt.anchoredPosition = Vector2.zero;
				img.transform.localScale = new Vector3(Mathf.Abs(img.transform.localScale.x), img.transform.localScale.y, 1f);
				Vector2 s = rt.sizeDelta;
				float max = Mathf.Max(s.x, s.y);
				if (max > 36f)
				{
					rt.sizeDelta = s * (36f / max);
				}
				b.button1.mainText.rectTransform.anchoredPosition = new Vector2(52f, 0f);
				Thing tip = r.icon;
				b.button1.SetTooltip("note", delegate(UITooltip tt)
				{
					tip.WriteNote(tt.note);
				});
			}
			else
			{
				b.button1.mainText.rectTransform.anchoredPosition = new Vector2(30f, 0f);
			}
			MenuExtras.ApplyNameDisplay(b.button1.mainText, 520f);
		}

		// Rebuild rows in place after an action on a refreshable menu.
		private static void Refresh()
		{
			if (current == null || current.refreshSource == null || !Alive)
			{
				return;
			}
			current.refreshSource();
			if (current.rows.Count == 0)
			{
				Back();
			}
			else
			{
				page = Mathf.Clamp(page, 0, PageCount - 1);
				ApplyPage();
			}
		}

		private static void Activate(MenuRow r)
		{
			try
			{
				if (r.maxAmount > 1 && r.onAmount != null)
				{
					// enter the count spinner instead of acting immediately
					amountRow = r;
					amount = r.maxAmount;
					sel = pageRows.IndexOf(r);
					ApplyPage();
					return;
				}
				Run(r, (r.onAmount != null) ? ((Action)delegate { r.onAmount(1); }) : r.act);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys action failed: " + ex);
			}
		}

		private static void Run(MenuRow r, Action action)
		{
			if (action == null)
			{
				return;
			}
			if (r.closeOnAct)
			{
				Close();
			}
			action();
			if (!r.closeOnAct)
			{
				Refresh();
			}
		}

		private static void ApplyPage()
		{
			if (!Alive || current == null)
			{
				return;
			}
			view = new List<MenuRow>(current.rows);
			if (sortMode == 1)
			{
				view.Sort((MenuRow a, MenuRow b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
			}
			else if (sortMode == 2)
			{
				view.Sort((MenuRow a, MenuRow b) => b.sortWeight.CompareTo(a.sortWeight));
			}
			pageRows.Clear();
			int start = page * PageSize;
			for (int i = start; i < view.Count && i < start + PageSize; i++)
			{
				pageRows.Add(view[i]);
			}
			sel = Mathf.Clamp(sel, 0, Mathf.Max(0, pageRows.Count - 1));
			layer.list.List();
			layer.RefreshSize();
			string caption = current.title;
			if (PageCount > 1)
			{
				caption += "  (" + (page + 1) + "/" + PageCount + ") ◄►";
			}
			if (sortMode != 0)
			{
				caption += (sortMode == 1) ? " [name]" : " [weight]";
			}
			layer.windows[0].SetCaption(caption);
			ShowSelTooltip();
		}

		// keep the item stat card visible for the keyboard-selected row
		private static void ShowSelTooltip()
		{
			try
			{
				TooltipManager.Instance.HideTooltips(immediate: true);
				if (sel < 0 || sel >= pageRows.Count || pageRows[sel].icon == null || layer.list.buttons == null || sel >= layer.list.buttons.Count)
				{
					return;
				}
				ItemGeneral item = layer.list.buttons[sel].component as ItemGeneral;
				if ((bool)item)
				{
					TooltipManager.Instance.disableHide = "note"; // LayerList.OnKill clears it
					item.button1.ShowTooltipForced(ignoreWhenRightClick: false);
				}
			}
			catch
			{
				// cosmetic only
			}
		}

		// ---------- keyboard, driven from MenuExtras.OnUpdate ----------

		public static void HandleKeys()
		{
			if (!Alive || current == null)
			{
				return;
			}
			// never react to the keystroke that opened this menu
			if (Time.frameCount - openFrame < 2 || EInput.waitReleaseAnyKey)
			{
				return;
			}
			// another window (the examine card, etc.) sits ON TOP of our menu:
			// route Shift/x to it and leave the menu underneath alone
			Layer top = (EClass.ui != null) ? EClass.ui.TopLayer : null;
			if (top != null && (object)top != (object)layer)
			{
				if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.X))
				{
					top.Close();
				}
				shiftHeld = false;
				return;
			}
			// x examines the hovered/selected row (press again to close)
			if (Input.GetKeyDown(KeyCode.X) && !EInput.isCtrlDown && !EInput.isAltDown)
			{
				Thing t = MenuExtras.HoveredThing() ?? SelectedThing();
				if (t != null)
				{
					EClass.ui.AddLayerDontCloseOthers<LayerInfo>().SetThing(t);
				}
				return;
			}
			// Shift alone = back/close, like Elona. Fires on release so that
			// shift+digit (hotkey assignment) still works inside menus.
			if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
			{
				shiftHeld = true;
				shiftUsed = false;
			}
			else if (shiftHeld && Input.anyKeyDown)
			{
				shiftUsed = true;
			}
			if ((Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) && shiftHeld)
			{
				shiftHeld = false;
				if (!shiftUsed)
				{
					Back();
					return;
				}
			}
			if (amountRow != null)
			{
				HandleAmountKeys();
				return;
			}
			if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.PageDown))
			{
				if (PageCount > 1)
				{
					page = (page + 1) % PageCount;
					sel = 0;
					ApplyPage();
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.PageUp))
			{
				if (PageCount > 1)
				{
					page = (page - 1 + PageCount) % PageCount;
					sel = 0;
					ApplyPage();
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				sel = (sel + 1) % Mathf.Max(1, pageRows.Count);
				ApplyPage();
				return;
			}
			if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				sel = (sel - 1 + Mathf.Max(1, pageRows.Count)) % Mathf.Max(1, pageRows.Count);
				ApplyPage();
				return;
			}
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				if (sel >= 0 && sel < pageRows.Count)
				{
					Activate(pageRows[sel]);
				}
				return;
			}
			// typed keys: z = sort, letters = rows (lowercase only)
			string typed = Input.inputString;
			if (!string.IsNullOrEmpty(typed) && !EInput.isShiftDown && !EInput.isCtrlDown && !EInput.isAltDown)
			{
				char c = typed[0];
				if (c == 'z')
				{
					sortMode = (sortMode + 1) % 3;
					sel = 0;
					page = 0;
					ApplyPage();
					return;
				}
				int index = Letters.IndexOf(c);
				if (index >= 0 && index < pageRows.Count && index < PageSize)
				{
					Activate(pageRows[index]);
				}
			}
		}

		private static void HandleAmountKeys()
		{
			MenuRow r = amountRow;
			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				amount = (amount % r.maxAmount) + 1;
				ApplyPage();
			}
			else if (Input.GetKeyDown(KeyCode.LeftArrow))
			{
				amount = ((amount - 2 + r.maxAmount) % r.maxAmount) + 1;
				ApplyPage();
			}
			else if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				amount = Mathf.Min(r.maxAmount, amount + 10);
				ApplyPage();
			}
			else if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				amount = Mathf.Max(1, amount - 10);
				ApplyPage();
			}
			else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				int n = amount;
				amountRow = null;
				try
				{
					Run(r, delegate { r.onAmount(n); });
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys amount action failed: " + ex);
				}
			}
		}

		public static Thing SelectedThing()
		{
			if (Alive && sel >= 0 && sel < pageRows.Count)
			{
				return pageRows[sel].icon;
			}
			return null;
		}
	}
}
