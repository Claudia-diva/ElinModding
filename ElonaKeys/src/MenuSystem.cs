using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// The Elona-style menu window: paginated, fully keyboard-navigable.
	// - fixed rows per page (MenuPageSize config), everything fits on screen
	// - left/right (or PageUp/Down) switch pages
	// - up/down move the ► cursor, Enter activates it
	// - a..o letter per row (typed letter activates), click also works
	// - row text carries the letter itself ("a) bread") so nothing can overlap
	public class MenuRow
	{
		public string label;
		public Thing icon;
		public Action act;
		public bool closeOnAct = true;
	}

	public static class MenuSystem
	{
		private const string Letters = "abcdefghijklmnopqrstuvwxyz";

		private static LayerList layer;
		private static string title;
		private static List<MenuRow> all = new List<MenuRow>();
		private static readonly List<MenuRow> pageRows = new List<MenuRow>();
		private static int page;
		private static int sel;
		private static int openFrame;
		private static Action refreshSource; // optional: rebuilds `all` after an action

		public static bool Alive => layer != null && (bool)layer;

		// max 20 rows so x (examine) and z stay free as letters
		private static int PageSize => Mathf.Clamp(Plugin.MenuPageSize, 5, 20);

		private static int PageCount => Mathf.Max(1, (all.Count + PageSize - 1) / PageSize);

		public static void Show(string _title, List<MenuRow> rows, Action _refreshSource = null)
		{
			title = _title;
			all = rows;
			refreshSource = _refreshSource;
			page = 0;
			sel = 0;
			openFrame = Time.frameCount;
			layer = EClass.ui.AddLayer<LayerList>();
			layer.SetSize(640f);
			layer.SetList2(pageRows, RowText, delegate(MenuRow r, ItemGeneral b)
			{
				Activate(r);
			}, BuildRow, autoClose: false);
			ApplyPage();
			MenuExtras.RegisterMenu(layer);
		}

		public static void Close()
		{
			if (Alive)
			{
				layer.Close();
			}
			layer = null;
		}

		public static void SetRows(List<MenuRow> rows)
		{
			all = rows;
		}

		private static string RowText(MenuRow r)
		{
			int i = pageRows.IndexOf(r);
			string cursor = (i == sel) ? "► " : "   ";
			string letter = (i >= 0 && i < Letters.Length) ? (Letters[i] + ")  ") : "";
			return cursor + letter + r.label;
		}

		private static void BuildRow(MenuRow r, ItemGeneral b)
		{
			if ((bool)b.button1.keyText)
			{
				// letters live inside the label; kill the key-chip visual entirely
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
				// pivots; this mold has neither. Normalize so the sprite actually
				// shows inside the small slot.
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
			MenuExtras.ApplyNameDisplay(b.button1.mainText, 480f);
		}

		// Called after an action on a refreshable menu (drop/wear): rebuild rows
		// in place, keep the page, close when nothing is left.
		private static void Refresh()
		{
			if (refreshSource == null || !Alive)
			{
				return;
			}
			refreshSource();
			if (all.Count == 0)
			{
				Close();
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
				if (r.closeOnAct)
				{
					Close();
				}
				r.act();
				if (!r.closeOnAct)
				{
					Refresh();
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys action failed: " + ex);
			}
		}

		private static void ApplyPage()
		{
			if (!Alive)
			{
				return;
			}
			pageRows.Clear();
			int start = page * PageSize;
			for (int i = start; i < all.Count && i < start + PageSize; i++)
			{
				pageRows.Add(all[i]);
			}
			sel = Mathf.Clamp(sel, 0, Mathf.Max(0, pageRows.Count - 1));
			layer.list.List();
			layer.RefreshSize();
			string caption = title;
			if (PageCount > 1)
			{
				caption += "  (" + (page + 1) + "/" + PageCount + ")  ◄ ►";
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
					// stop the manager from fading the card when the mouse isn't
					// hovering; LayerList.OnKill clears disableHide on close
					TooltipManager.Instance.disableHide = "note";
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
			if (!Alive)
			{
				return;
			}
			// never react to the same keystroke that opened this menu
			if (Time.frameCount - openFrame < 2 || EInput.waitReleaseAnyKey)
			{
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
			// typed letter selects the row (lowercase only; capitals stay free)
			string typed = Input.inputString;
			if (!string.IsNullOrEmpty(typed) && !EInput.isShiftDown && !EInput.isCtrlDown && !EInput.isAltDown)
			{
				char c = typed[0];
				int index = Letters.IndexOf(c);
				if (index >= 0 && index < pageRows.Count)
				{
					Activate(pageRows[index]);
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
