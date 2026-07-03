using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElonaKeys
{
	// The Elona-style menu window: paginated, fully keyboard-navigable.
	// - fixed rows per page (MenuPageSize config), so everything fits on screen
	// - left/right (or PageUp/Down) switch pages
	// - up/down move the selection, Enter activates it
	// - a..z letter per row, click also works
	// - row layout: [icon] [letter] [name], rich tooltip, x to examine
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
		private static Action refreshSource; // optional: rebuilds `all` after an action

		public static bool Alive => layer != null && (bool)layer;

		private static int PageSize => Mathf.Max(5, Plugin.MenuPageSize);

		private static int PageCount => Mathf.Max(1, (all.Count + PageSize - 1) / PageSize);

		public static void Show(string _title, List<MenuRow> rows, Action _refreshSource = null)
		{
			title = _title;
			all = rows;
			refreshSource = _refreshSource;
			page = 0;
			sel = 0;
			layer = EClass.ui.AddLayer<LayerList>();
			layer.SetSize(640f);
			layer.SetList2(pageRows, (MenuRow r) => r.label, delegate(MenuRow r, ItemGeneral b)
			{
				Activate(r);
			}, delegate(MenuRow r, ItemGeneral b)
			{
				BuildRow(r, b, pageRows.IndexOf(r));
			}, autoClose: false);
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

		public static void SetRows(List<MenuRow> rows)
		{
			all = rows;
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

		private static void BuildRow(MenuRow r, ItemGeneral b, int index)
		{
			// [icon @24] [letter @52] [name @72]
			if (r.icon != null)
			{
				b.card = r.icon;
				RectTransform iconParent = b.button1.icon.transform.parent as RectTransform;
				iconParent.SetActive(enable: true);
				iconParent.anchoredPosition = new Vector2(24f, 0f);
				r.icon.SetImage(b.button1.icon);
				Thing tip = r.icon;
				b.button1.SetTooltip("note", delegate(UITooltip tt)
				{
					tip.WriteNote(tt.note);
				});
			}
			if ((bool)b.button1.keyText)
			{
				b.button1.keyText.text = (index < Letters.Length) ? Letters[index].ToString() : "";
				(b.button1.keyText.transform as RectTransform).anchoredPosition = new Vector2(52f, 0f);
			}
			b.button1.mainText.rectTransform.anchoredPosition = new Vector2(72f, 0f);
			MenuExtras.ApplyNameDisplay(b.button1.mainText, 470f);
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
			layer.list.List();
			layer.RefreshSize();
			string caption = title;
			if (PageCount > 1)
			{
				caption += "  (" + (page + 1) + "/" + PageCount + ")  ◄ ►";
			}
			layer.windows[0].SetCaption(caption);
			sel = Mathf.Clamp(sel, 0, pageRows.Count - 1);
			HighlightSel();
		}

		// ---------- keyboard navigation, driven from MenuExtras.OnUpdate ----------

		public static void HandleKeys()
		{
			if (!Alive)
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
			}
			else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.PageUp))
			{
				if (PageCount > 1)
				{
					page = (page - 1 + PageCount) % PageCount;
					sel = 0;
					ApplyPage();
				}
			}
			else if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				sel = (sel + 1) % Mathf.Max(1, pageRows.Count);
				HighlightSel();
			}
			else if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				sel = (sel - 1 + Mathf.Max(1, pageRows.Count)) % Mathf.Max(1, pageRows.Count);
				HighlightSel();
			}
			else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				if (sel >= 0 && sel < pageRows.Count)
				{
					Activate(pageRows[sel]);
				}
			}
		}

		private static void HighlightSel()
		{
			if (!Alive || layer.list.buttons == null)
			{
				return;
			}
			for (int i = 0; i < layer.list.buttons.Count; i++)
			{
				ItemGeneral item = layer.list.buttons[i].component as ItemGeneral;
				if (!item)
				{
					continue;
				}
				if (i == sel)
				{
					EInput.Select(item.button1.gameObject);
					item.button1.DoHighlightTransition();
				}
				else
				{
					item.button1.DoNormalTransition();
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
