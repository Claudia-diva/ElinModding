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
		public string rightText;             // usually the weight column
		public int sortWeight;               // for the weight sort mode
		public int maxAmount;                // >1 enables the count spinner
		public Action<int> onAmount;         // called with the chosen count
		public SourceElement.Row actSrc;     // ability rows: enables digit hotkeying
		public Element ability;              // ability rows: element icon + native tooltip
		public string fixedPrefix;           // static text (slot names); label scrolls separately
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

		public static void Show(string _title, List<MenuRow> rows, Action _refreshSource = null, bool keepLayers = false)
		{
			MenuCycle.ClearUnlessJustSet(); // Tab only cycles inside a window family
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
				layer = keepLayers ? EClass.ui.AddLayerDontCloseOthers<LayerList>() : EClass.ui.AddLayer<LayerList>();
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
			string spin = (r == amountRow) ? ("   « " + amount + " (" + r.maxAmount + ") »") : "";
			if (r.fixedPrefix != null)
			{
				// the scrolling name lives in a separate child text (EKName)
				return cursor + r.fixedPrefix + spin;
			}
			return cursor + r.label + spin;
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
			int index = pageRows.IndexOf(r);
			string letter = (index >= 0 && index < Letters.Length) ? Letters[index].ToString() : "";
			// Elona layout: [sprite @16] [letter badge @46] [name @78] [weight right]
			bool badge = false;
			if ((bool)b.button1.keyText)
			{
				// the |_\ chip is the letter badge (keyText's container): revive it,
				// give it its letter back, park it right of the sprite
				b.button1.keyText.SetActive(enable: true);
				b.button1.keyText.text = letter;
				RectTransform chip = b.button1.keyText.transform as RectTransform;
				if (chip.parent != null && chip.parent != b.button1.transform && chip.parent.GetComponent<ItemGeneral>() == null)
				{
					chip = chip.parent as RectTransform;
				}
				chip.anchoredPosition = new Vector2(46f, 0f);
				badge = true;
			}
			if (r.icon != null)
			{
				b.card = r.icon;
				RectTransform iconParent = b.button1.icon.transform.parent as RectTransform;
				iconParent.SetActive(enable: true);
				iconParent.anchoredPosition = new Vector2(16f, 0f);
				Image img = b.button1.icon;
				// hide slot chrome living in the icon container, sprite on top
				foreach (Image sib in iconParent.GetComponentsInChildren<Image>(includeInactive: true))
				{
					if (sib != img)
					{
						sib.enabled = false;
					}
				}
				img.enabled = true;
				img.transform.SetAsLastSibling();
				r.icon.SetImage(img);
				// RenderRow.SetImage encodes the material tint for the game's item
				// shader. Borrow that material from a live inventory button so the
				// tint decodes correctly; fall back to plain white without it.
				Material itemMat = ItemMaterial();
				if (itemMat != null)
				{
					img.material = itemMat;
				}
				else
				{
					img.color = Color.white;
				}
				RectTransform rt = img.rectTransform;
				rt.pivot = new Vector2(0.5f, 0.5f);
				rt.anchoredPosition = Vector2.zero;
				img.transform.localScale = new Vector3(Mathf.Abs(img.transform.localScale.x), img.transform.localScale.y, 1f);
				Vector2 s = rt.sizeDelta;
				float max = Mathf.Max(s.x, s.y);
				if (max > 34f)
				{
					rt.sizeDelta = s * (34f / max);
				}
				Thing tip = r.icon;
				b.button1.SetTooltip("note", delegate(UITooltip tt)
				{
					tip.WriteNote(tt.note);
				});
			}
			else if (r.ability != null)
			{
				// spell/action rows: element sprite (plain UI sprite, no item
				// shader) + the game's own ability tooltip (ButtonAbility recipe)
				Element ab = r.ability;
				RectTransform iconParent = b.button1.icon.transform.parent as RectTransform;
				iconParent.SetActive(enable: true);
				iconParent.anchoredPosition = new Vector2(16f, 0f);
				Image img = b.button1.icon;
				foreach (Image sib in iconParent.GetComponentsInChildren<Image>(includeInactive: true))
				{
					if (sib != img)
					{
						sib.enabled = false;
					}
				}
				img.enabled = true;
				img.transform.SetAsLastSibling();
				img.material = null; // pooled row may carry the item shader
				// Act.SetImage = the real ability sprite (+element tint);
				// Element.SetImage would draw the scaling-stat icon instead
				bool drawn = false;
				try
				{
					Act a2 = ACT.Create(ab.source);
					if (a2 != null)
					{
						a2.SetImage(img);
						drawn = true;
					}
				}
				catch
				{
				}
				if (!drawn)
				{
					ab.SetImage(img);
					img.color = Color.white;
				}
				RectTransform art = img.rectTransform;
				art.pivot = new Vector2(0.5f, 0.5f);
				art.anchoredPosition = Vector2.zero;
				img.transform.localScale = new Vector3(Mathf.Abs(img.transform.localScale.x), img.transform.localScale.y, 1f);
				Vector2 asz = art.sizeDelta;
				float amax = Mathf.Max(asz.x, asz.y);
				if (amax > 34f || amax <= 0f)
				{
					art.sizeDelta = (amax > 0f) ? (asz * (34f / amax)) : new Vector2(30f, 30f);
				}
				b.button1.SetTooltip("note", delegate(UITooltip tt)
				{
					ab.WriteNote(tt.note, EClass.pc.elements, delegate
					{
						Act a = ACT.Create(ab.source);
						ab._WriteNote(tt.note, EClass.pc, a);
					});
				});
			}
			if (!badge)
			{
				// no badge on this mold: letter goes into the text itself
				b.button1.mainText.text = letter + ")  " + b.button1.mainText.text;
			}
			b.button1.mainText.rectTransform.anchoredPosition = new Vector2(badge ? 78f : 52f, 0f);
			SetWeightText(b, r.rightText);
			if (r.fixedPrefix != null)
			{
				// static prefix in mainText; only the name (child text) scrolls
				SetNameText(b, r.label, 150f + (badge ? 78f : 52f));
			}
			else
			{
				SetNameText(b, null, 0f);
				MenuExtras.ApplyNameDisplay(b.button1.mainText, 430f);
			}
		}

		// scrolling name child for fixedPrefix rows (pooled-row safe)
		private static void SetNameText(ItemGeneral b, string text, float x)
		{
			try
			{
				Transform existing = b.button1.transform.Find("EKName");
				UIText nt = (existing != null) ? existing.GetComponent<UIText>() : null;
				if (text == null)
				{
					if ((bool)nt)
					{
						nt.text = "";
					}
					return;
				}
				if (nt == null)
				{
					GameObject go = new GameObject("EKName");
					go.transform.SetParent(b.button1.transform, worldPositionStays: false);
					nt = go.AddComponent<UIText>();
					nt.font = b.button1.mainText.font;
					nt.fontSize = b.button1.mainText.fontSize;
					nt.alignment = TextAnchor.MiddleLeft;
					nt.horizontalOverflow = HorizontalWrapMode.Overflow;
					nt.verticalOverflow = VerticalWrapMode.Overflow;
					nt.raycastTarget = false;
					RectTransform nrt = nt.rectTransform;
					nrt.anchorMin = new Vector2(0f, 0.5f);
					nrt.anchorMax = new Vector2(0f, 0.5f);
					nrt.pivot = new Vector2(0f, 0.5f);
					nrt.sizeDelta = new Vector2(300f, 24f);
				}
				nt.color = b.button1.mainText.color;
				nt.rectTransform.anchoredPosition = new Vector2(x, 0f);
				nt.text = text;
				MenuExtras.ApplyNameDisplay(nt, 280f);
			}
			catch
			{
				// cosmetic only
			}
		}

		// the canonical shared UI item material (CoreRef.matUIObj): decodes the
		// encoded tint from RenderRow.SetImage. Never borrow from a live button;
		// their instances carry per-save/per-state properties (darkened sprites).
		private static Material ItemMaterial()
		{
			try
			{
				return EClass.core.refs.matUIObj;
			}
			catch
			{
				return null;
			}
		}

		// right-aligned weight column: our own Text child, pooled-row safe
		private static void SetWeightText(ItemGeneral b, string text)
		{
			try
			{
				Transform existing = b.button1.transform.Find("EKWeight");
				Text wt;
				if (existing != null)
				{
					wt = existing.GetComponent<Text>();
				}
				else
				{
					GameObject go = new GameObject("EKWeight");
					go.transform.SetParent(b.button1.transform, worldPositionStays: false);
					wt = go.AddComponent<Text>();
					wt.font = b.button1.mainText.font;
					wt.fontSize = 15;
					wt.alignment = TextAnchor.MiddleRight;
					wt.color = new Color(0.45f, 0.42f, 0.32f);
					wt.raycastTarget = false;
					RectTransform wrt = wt.rectTransform;
					wrt.anchorMin = new Vector2(1f, 0.5f);
					wrt.anchorMax = new Vector2(1f, 0.5f);
					wrt.pivot = new Vector2(1f, 0.5f);
					wrt.sizeDelta = new Vector2(90f, 24f);
					wrt.anchoredPosition = new Vector2(-14f, 0f);
				}
				if ((bool)wt)
				{
					wt.text = text ?? "";
				}
			}
			catch
			{
				// cosmetic only
			}
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
			// Elona-style totals for item lists: piece count + total weight
			int pieces = 0;
			int grams = 0;
			foreach (MenuRow r in view)
			{
				if (r.icon != null)
				{
					pieces++; // unique entries, not stack counts (a million gold = 1)
					grams += r.sortWeight;
				}
			}
			if (pieces > 0)
			{
				// plain text: the caption UIText doesn't parse rich-text tags
				caption += "  · " + pieces + " items  " + (grams / 1000f).ToString("0.0") + "s";
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
				if (sel < 0 || sel >= pageRows.Count || (pageRows[sel].icon == null && pageRows[sel].ability == null) || layer.list.buttons == null || sel >= layer.list.buttons.Count)
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
			// Tab cycles the window family (items / equipment-chara / spells-actions)
			if (Input.GetKeyDown(KeyCode.Tab) && MenuCycle.Next())
			{
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
			if (KeyRepeat.Down(KeyCode.RightArrow) || KeyRepeat.Down(KeyCode.PageDown))
			{
				if (PageCount > 1)
				{
					page = (page + 1) % PageCount;
					sel = 0;
					ApplyPage();
				}
				return;
			}
			if (KeyRepeat.Down(KeyCode.LeftArrow) || KeyRepeat.Down(KeyCode.PageUp))
			{
				if (PageCount > 1)
				{
					page = (page - 1 + PageCount) % PageCount;
					sel = 0;
					ApplyPage();
				}
				return;
			}
			if (KeyRepeat.Down(KeyCode.DownArrow))
			{
				sel = (sel + 1) % Mathf.Max(1, pageRows.Count);
				ApplyPage();
				return;
			}
			if (KeyRepeat.Down(KeyCode.UpArrow))
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
			// z = sort. Row letters are handled NATIVELY by LayerList via keyText
			// (adding our own letter handler would double-activate rows).
			string typed = Input.inputString;
			if (typed == "z" && !EInput.isShiftDown && !EInput.isCtrlDown && !EInput.isAltDown)
			{
				sortMode = (sortMode + 1) % 3;
				sel = 0;
				page = 0;
				ApplyPage();
			}
		}

		private static void HandleAmountKeys()
		{
			MenuRow r = amountRow;
			if (KeyRepeat.Down(KeyCode.RightArrow))
			{
				amount = (amount % r.maxAmount) + 1;
				ApplyPage();
			}
			else if (KeyRepeat.Down(KeyCode.LeftArrow))
			{
				amount = ((amount - 2 + r.maxAmount) % r.maxAmount) + 1;
				ApplyPage();
			}
			else if (KeyRepeat.Down(KeyCode.UpArrow))
			{
				amount = Mathf.Min(r.maxAmount, amount + 10);
				ApplyPage();
			}
			else if (KeyRepeat.Down(KeyCode.DownArrow))
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

		public static SourceElement.Row SelectedActSrc()
		{
			if (Alive && sel >= 0 && sel < pageRows.Count)
			{
				return pageRows[sel].actSrc;
			}
			return null;
		}
	}
}
