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
		public Action<UINote> note;          // custom tooltip body (crafting recipes...)
		public Card iconCard;                // NPC/chara rows: live sprite in the icon slot
		public Action<Image> drawIcon;       // custom icon painter (recipe sprites...)
		public float rightWidth;             // right column width override (0 = WeightWidth)
		public int sortCount;                // count/stock sort key (item Num, spell stock)
		public string[] cols;                // aligned right-side columns (ability rows: Lv/Hit/Dmg/Cost/Left)
		public bool pinTop;                  // stays first in every sort mode (the held item)
		public bool pinBottom;               // stays last in every sort mode (utility rows)
		public Action altAct;                // numpad0 on rows WITHOUT a spinner (hold in hands...)
	}

	public static class MenuSystem
	{
		private const string Letters = "abcdefghijklmnopqrstuvwxyz";

		private class Screen
		{
			public string title;
			public List<MenuRow> rows;
			public Action refreshSource;
			public Action<int> sourceCycle; // z/x tab/source switching, per screen
			public string footerExtra;      // extra footer detail (container slots...)
			public Action onTab;            // Tab behavior override (shop buy<->sell)
			public Action<int> onSwap;      // period/comma source cycling (browser)
			public string rightHeader;      // right column header ("Weight" default)
		}

		// extra footer detail for the NEXT Show() call (consumed once)
		public static string NextFooterExtra;
		// Tab override for the NEXT Show() call (consumed once). ONLY for
		// menus outside the window families (shop buy/sell, ally trade):
		// inside a family Tab must keep hopping windows.
		public static Action NextOnTab;
		// source swap (period = next, comma = prev) for the NEXT Show() call:
		// the family-safe way to cycle inventories/sources (browser)
		public static Action<int> NextOnSwap;
		// right-column header for the NEXT Show() call (consumed once)
		public static string NextRightHeader;

		// live footer update for refreshable menus (money after a purchase...)
		public static void SetFooterExtra(string s)
		{
			if (current != null)
			{
				current.footerExtra = s;
			}
		}

		// configurable page-turn sound (PageSound config; "none" silences)
		private static void PageSound()
		{
			try
			{
				string id = Plugin.PageSound;
				if (!string.IsNullOrEmpty(id) && id != "none")
				{
					SE.Play(id);
				}
			}
			catch
			{
			}
		}

		// per-menu cursor memory: reopening Drop/Cast/etc lands where you left
		// it (clamped by ApplyPage if rows shrank)
		private static readonly Dictionary<string, (int page, int sel)> cursorMemo = new Dictionary<string, (int page, int sel)>();

		private static LayerList layer;
		private static Screen current;
		private static readonly List<Screen> history = new List<Screen>();
		private static readonly List<MenuRow> pageRows = new List<MenuRow>();
		private static List<MenuRow> view = new List<MenuRow>();
		private static int page;
		private static int sel;
		private static int sortMode; // 0 original, 1 name, 2 weight/category, 3 count/stock
		private static int openFrame;

		// count spinner state
		private static MenuRow amountRow;
		private static int amount;

		// hold-x-to-reset tracking (sourceCycle screens)
		private static float xDownAt;
		private static bool xPending;

		// multi-source screens (inventory browser, craft categories): z/x cycle
		// sources/tabs instead of sort/examine. Stored ON the screen, so shift-
		// back restores the right behavior per window.
		public static void SetSourceCycle(Action<int> cb)
		{
			if (current != null)
			{
				current.sourceCycle = cb;
			}
		}

		// shift = back, but only if shift wasn't a modifier for something else
		private static bool shiftHeld;
		private static bool shiftUsed;

		public static bool Alive => layer != null && (bool)layer;

		// max 20 rows so x (examine) and z (sort) stay free as letters
		private static int PageSize => Mathf.Clamp(Plugin.MenuPageSize, 5, 20);

		private static int PageCount => Mathf.Max(1, (view.Count + PageSize - 1) / PageSize);

		public static void Show(string _title, List<MenuRow> rows, Action _refreshSource = null, bool keepLayers = false, bool replace = false)
		{
			MenuCycle.ClearUnlessJustSet(); // Tab only cycles inside a window family
			if (Alive && current != null)
			{
				if (!replace)
				{
					history.Add(current); // shift returns here
				}
				// replace = tab/source cycling: same window, new view; shift
				// exits instead of walking every tab you visited
			}
			else
			{
				history.Clear();
			}
			current = new Screen { title = _title, rows = rows, refreshSource = _refreshSource, footerExtra = NextFooterExtra, onTab = NextOnTab, onSwap = NextOnSwap, rightHeader = NextRightHeader };
			NextFooterExtra = null;
			NextOnTab = null;
			NextOnSwap = null;
			NextRightHeader = null;
			page = 0;
			sel = 0;
			if (cursorMemo.TryGetValue(_title, out var memo))
			{
				page = memo.page;
				sel = memo.sel;
			}
			amountRow = null;
			openFrame = Time.frameCount;
			if (!Alive)
			{
				layer = keepLayers ? EClass.ui.AddLayerDontCloseOthers<LayerList>() : EClass.ui.AddLayer<LayerList>();
				layer.paddings = new Vector2(layer.paddings.x, layer.paddings.y + 22f); // footer line space
				layer.SetSize(Plugin.MenuWidth);
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
			// one-shot Show() parameters must not leak onto an unrelated menu
			NextFooterExtra = null;
			NextOnTab = null;
			NextOnSwap = null;
			NextRightHeader = null;
			// hold-v's armed manual mode: keep it ONLY when this close is part
			// of a row-action dispatch (Run closes BEFORE the action fires and
			// CastAbility consumes the flag); an Escape/shift/Tab close clears
			// it, or a later quickbind cast would silently aim manually once
			if (Time.frameCount != dispatchFrame)
			{
				Handlers.ManualCastMode = false;
			}
			if (Alive)
			{
				layer.Close();
			}
			layer = null;
		}

		// run something when THIS menu's window dies, whatever kills it
		// (Escape, shift, Tab family hop): the shop menu closes the trade with it
		public static void SetOnKill(Action a)
		{
			if (Alive && a != null)
			{
				layer.SetOnKill(a);
			}
		}

		// one step back in the window chain (what shift does), for submenus
		// that want to return to their parent after acting
		public static void BackOne()
		{
			Back();
		}

		// re-run the current screen's row builder (live refresh after an action)
		public static void RefreshCurrent()
		{
			if (current != null && current.refreshSource != null)
			{
				try
				{
					current.refreshSource();
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys menu refresh failed: " + ex);
				}
			}
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
			else if (r.iconCard != null)
			{
				// characters in interaction menus: live sprite, same slot
				Card c = r.iconCard;
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
				try
				{
					c.SetImage(img);
					Material itemMat2 = ItemMaterial();
					if (itemMat2 != null)
					{
						img.material = itemMat2;
					}
					else
					{
						img.color = Color.white;
					}
					RectTransform crt = img.rectTransform;
					crt.pivot = new Vector2(0.5f, 0.5f);
					crt.anchoredPosition = Vector2.zero;
					img.transform.localScale = new Vector3(Mathf.Abs(img.transform.localScale.x), img.transform.localScale.y, 1f);
					Vector2 cs = crt.sizeDelta;
					float cmax = Mathf.Max(cs.x, cs.y);
					if (cmax > 34f)
					{
						crt.sizeDelta = cs * (34f / cmax);
					}
					Card tipC = c;
					b.button1.SetTooltip("note", delegate(UITooltip tt)
					{
						tipC.WriteNote(tt.note);
					});
				}
				catch
				{
					img.enabled = false;
				}
			}
			else if (r.drawIcon != null)
			{
				// custom painter (crafting recipe product sprites...)
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
				try
				{
					img.material = null;
					r.drawIcon(img);
					RectTransform drt = img.rectTransform;
					drt.pivot = new Vector2(0.5f, 0.5f);
					drt.anchoredPosition = Vector2.zero;
					img.transform.localScale = new Vector3(Mathf.Abs(img.transform.localScale.x), img.transform.localScale.y, 1f);
					Vector2 ds = drt.sizeDelta;
					float dmax = Mathf.Max(ds.x, ds.y);
					if (dmax > 34f || dmax <= 0f)
					{
						drt.sizeDelta = (dmax > 0f) ? (ds * (34f / dmax)) : new Vector2(30f, 30f);
					}
				}
				catch
				{
					img.enabled = false;
				}
			}
			else if (r.ability != null)
			{
				// spell/action rows: exactly what the ability tooltip header does
				// (Element.AddHeaderAbility): Act.SetImage on a plain image over
				// the parchment row. No extra backing — a dark square behind the
				// tinted glyph deep-fries it.
				Element ab = r.ability;
				RectTransform iconParent = b.button1.icon.transform.parent as RectTransform;
				iconParent.SetActive(enable: true);
				iconParent.anchoredPosition = new Vector2(16f, 0f);
				// bare Image + (element as Act).SetImage: the setup that
				// PROVABLY renders the art (the HeaderAbility-prefab clone of
				// 1.25.1 carried its own row layout and drew nothing here)
				Transform ownT = iconParent.Find("EKAbilityIcon");
				Image img = (ownT != null) ? ownT.GetComponent<Image>() : null;
				if (img == null)
				{
					GameObject io = new GameObject("EKAbilityIcon");
					io.transform.SetParent(iconParent, worldPositionStays: false);
					img = io.AddComponent<Image>();
					img.raycastTarget = false;
					RectTransform irt = img.rectTransform;
					irt.anchorMin = new Vector2(0.5f, 0.5f);
					irt.anchorMax = new Vector2(0.5f, 0.5f);
					irt.pivot = new Vector2(0.5f, 0.5f);
				}
				foreach (Image sib in iconParent.GetComponentsInChildren<Image>(includeInactive: true))
				{
					if (sib != img)
					{
						sib.enabled = false; // button icon, stale prefab clones, old backings
					}
				}
				// a leftover 1.25.1 prefab clone would survive as a disabled husk;
				// make sure its subtree stays dead
				Transform stale = iconParent.Find("EKAbilityHeader");
				if (stale != null)
				{
					stale.SetActive(enable: false);
				}
				// the UI-object material, same fix item icons needed years of
				// versions ago: some sprite tints are ENCODED for the game's
				// shader and render as black discs on the default material
				// (death funnel was black in the row, purple in the tooltip)
				img.material = ItemMaterial();
				img.enabled = true;
				img.transform.SetAsLastSibling();
				// AddHeaderAbility paints with THE ELEMENT ITSELF as an Act
				// ((this as Act)?.SetImage) — the pc's spells/actions ARE Act
				// instances with the real sprite. ACT.Create(source) built a
				// bare copy whose GetSprite() came up empty for spells, so
				// they all fell back to the default-ability glyph (the
				// identical tinted blobs). Created copy = fallback only.
				bool drawn = false;
				Act own = ab as Act;
				if (own != null)
				{
					try
					{
						own.SetImage(img);
						drawn = true;
					}
					catch
					{
					}
				}
				if (!drawn)
				{
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
				}
				if (!drawn)
				{
					ab.SetImage(img);
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
				SetStatIcon(b, ab);
			}
			else
			{
				SetStatIcon(b, null); // pooled rows: hide the stat badge
			}
			if (r.note != null)
			{
				Action<UINote> writer = r.note;
				b.button1.SetTooltip("note", delegate(UITooltip tt)
				{
					tt.note.Clear();
					writer(tt.note);
					tt.note.Build();
				});
			}
			if (!badge)
			{
				// no badge on this mold: letter goes into the text itself
				b.button1.mainText.text = letter + ")  " + b.button1.mainText.text;
			}
			float nameX = badge ? 78f : 52f;
			float rightW = (r.cols != null) ? ColsBlockWidth : ((r.rightWidth > 0f) ? r.rightWidth : Plugin.WeightWidth);
			b.button1.mainText.rectTransform.anchoredPosition = new Vector2(nameX, 0f);
			SetCols(b, r.cols);
			SetWeightText(b, (r.cols != null) ? "" : r.rightText, (r.cols != null) ? Plugin.WeightWidth : rightW);
			if (r.fixedPrefix != null)
			{
				// static prefix in mainText; only the name (child text) scrolls.
				// Prefix column is narrow (slot names are trimmed to 9 chars).
				float prefixW = 116f;
				SetNameText(b, r.label, nameX + prefixW, Plugin.MenuWidth - (nameX + prefixW) - rightW - 24f);
			}
			else
			{
				SetNameText(b, null, 0f, 0f);
				MenuExtras.ApplyNameDisplay(b.button1.mainText, Plugin.MenuWidth - nameX - rightW - 24f);
			}
		}

		// aligned right-side columns (Elona style). Canonical value order in
		// MenuRow.cols: [Lv, Attr, Pow, Hit, Dmg, Cost, Left]; the DISPLAY
		// order is the AbilityColumns config (csv, reorder or omit). Slot x
		// positions are computed from the ordered widths, right to left.
		private static readonly float[] ColW = { 44f, 52f, 44f, 52f, 68f, 56f, 44f };
		private static readonly string[] ColNames = { "Lv", "Attr", "Pow", "Hit", "Dmg", "Cost", "Left" };
		private static readonly string[] ColKeys = { "lv", "attr", "pow", "hit", "dmg", "cost", "left" };

		private static string orderSrc;      // config string the cache was built from
		private static int[] colOrder;       // display slot -> canonical index
		private static float[] colSlotX;     // display slot -> x offset from the right edge
		private static float colsBlock = 318f;

		private static int[] ColOrder()
		{
			string cfg = Plugin.AbilityColumns ?? "";
			if (cfg.Trim() == "lv,pow,hit,dmg,cost,left")
			{
				cfg = ""; // pre-Attr default saved in old configs: take the new full set
			}
			if (colOrder != null && orderSrc == cfg)
			{
				return colOrder;
			}
			List<int> order = new List<int>();
			foreach (string raw in cfg.Split(','))
			{
				int idx = Array.IndexOf(ColKeys, raw.Trim().ToLowerInvariant());
				if (idx >= 0 && !order.Contains(idx))
				{
					order.Add(idx);
				}
			}
			if (order.Count == 0)
			{
				for (int i = 0; i < ColKeys.Length; i++)
				{
					order.Add(i);
				}
			}
			colOrder = order.ToArray();
			colSlotX = new float[colOrder.Length];
			float x = -14f; // right padding
			for (int i = colOrder.Length - 1; i >= 0; i--)
			{
				colSlotX[i] = x;
				x -= ColW[colOrder[i]] + 8f;
			}
			colsBlock = -x + 8f;
			orderSrc = cfg;
			return colOrder;
		}

		private static float ColsBlockWidth
		{
			get
			{
				ColOrder();
				return colsBlock;
			}
		}

		private static void SetCols(ItemGeneral b, string[] cols)
		{
			try
			{
				int[] order = ColOrder();
				for (int i = 0; i < ColKeys.Length; i++)
				{
					Transform existing = b.button1.transform.Find("EKCol" + i);
					UIText ct = (existing != null) ? existing.GetComponent<UIText>() : null;
					if (cols == null || i >= order.Length)
					{
						if ((bool)ct)
						{
							ct.text = "";
						}
						continue;
					}
					if (ct == null)
					{
						GameObject go = new GameObject("EKCol" + i);
						go.transform.SetParent(b.button1.transform, worldPositionStays: false);
						ct = go.AddComponent<UIText>();
						ct.font = b.button1.mainText.font;
						ct.fontSize = 13;
						ct.alignment = TextAnchor.MiddleRight;
						ct.color = new Color(0.4f, 0.38f, 0.3f);
						ct.raycastTarget = false;
						ct.horizontalOverflow = HorizontalWrapMode.Overflow;
						RectTransform crt0 = ct.rectTransform;
						crt0.anchorMin = new Vector2(1f, 0.5f);
						crt0.anchorMax = new Vector2(1f, 0.5f);
						crt0.pivot = new Vector2(1f, 0.5f);
					}
					int canonical = order[i];
					RectTransform crt = ct.rectTransform;
					crt.sizeDelta = new Vector2(ColW[canonical], 22f);
					crt.anchoredPosition = new Vector2(colSlotX[i], 0f);
					ct.text = (canonical < cols.Length) ? (cols[canonical] ?? "") : "";
				}
			}
			catch
			{
				// cosmetic only
			}
		}

		// header labels for the column block, in the window's header strip.
		// mode: 0 = none, 1 = ability columns, 2 = single Weight header
		private static readonly List<Text> headerCols = new List<Text>();

		private static void RefreshColsHeader(int mode)
		{
			try
			{
				headerCols.RemoveAll((Text t) => t == null || !t);
				if (mode == 0)
				{
					foreach (Text t in headerCols)
					{
						t.enabled = false;
					}
					return;
				}
				// v4: NO prefab assumptions (v1 font was null, v2 got mask-
				// clipped, v3's buttonSort doesn't exist on LayerList windows).
				// Anchor to the FIRST ROW BUTTON's rendered geometry: its font
				// provably renders, its right edge is where the value columns
				// hang. Repositioned every refresh.
				if (layer == null || !layer || layer.windows.Count == 0 || layer.list == null || layer.list.buttons.Count == 0)
				{
					return;
				}
				ItemGeneral first = layer.list.buttons[0].component as ItemGeneral;
				if (first == null || !first || first.button1 == null || first.button1.mainText == null)
				{
					return;
				}
				RectTransform winRt = layer.windows[0].transform as RectTransform;
				RectTransform btnRt = first.button1.transform as RectTransform;
				Bounds bb = RectTransformUtility.CalculateRelativeRectTransformBounds(winRt, btnRt);
				// what this page shows: the ability block, or just the weight column
				int[] order = ColOrder();
				int slots = (mode == 1) ? order.Length : 1;
				if (headerCols.Count < slots)
				{
					for (int i = headerCols.Count; i < slots; i++)
					{
						GameObject go = new GameObject("EKColHead" + i);
						go.transform.SetParent(winRt, worldPositionStays: false);
						// the window root runs a layout group: without this the
						// labels get STACKED at the window bottom (the 1.24.1 bug)
						go.AddComponent<LayoutElement>().ignoreLayout = true;
						Text ht = go.AddComponent<Text>();
						ht.font = first.button1.mainText.font;
						ht.fontSize = 12;
						ht.alignment = TextAnchor.MiddleRight;
						ht.color = new Color(0.35f, 0.32f, 0.25f);
						ht.raycastTarget = false;
						headerCols.Add(ht);
					}
					if (Plugin.DebugLog)
					{
						Plugin.Log?.LogInfo("ElonaKeys cols header v4: created from row geometry (rowTop=" + bb.max.y + ", rowRight=" + bb.max.x + ")");
					}
				}
				for (int i = 0; i < headerCols.Count; i++)
				{
					Text ht = headerCols[i];
					if (i >= slots)
					{
						ht.enabled = false;
						continue;
					}
					float w;
					float x;
					if (mode == 1)
					{
						ht.text = ColNames[order[i]];
						w = ColW[order[i]];
						x = colSlotX[i];
					}
					else
					{
						ht.text = (current != null && !string.IsNullOrEmpty(current.rightHeader)) ? current.rightHeader : "Weight";
						w = Mathf.Max(Plugin.WeightWidth, 52f);
						x = -14f;
					}
					RectTransform rt = ht.rectTransform;
					// anchoredPosition space == the window's local space when the
					// anchors sit on the parent's pivot
					rt.anchorMin = winRt.pivot;
					rt.anchorMax = winRt.pivot;
					rt.pivot = new Vector2(1f, 0f);
					rt.sizeDelta = new Vector2(w, 16f);
					rt.anchoredPosition = new Vector2(bb.max.x + x, bb.max.y + 2f);
					ht.enabled = true;
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogWarning("ElonaKeys cols header failed: " + ex.Message);
			}
		}

		// small scaling-stat icon for ability rows (left of the stat column),
		// painted with the base Element.SetImage (the stat-icon painter)
		private static void SetStatIcon(ItemGeneral b, Element ability)
		{
			try
			{
				Transform existing = b.button1.transform.Find("EKStat");
				Image si = (existing != null) ? existing.GetComponent<Image>() : null;
				if (ability == null)
				{
					if ((bool)si)
					{
						si.enabled = false;
					}
					return;
				}
				float statX = -(ColsBlockWidth + 14f); // just left of the column block
				if (si == null)
				{
					GameObject go = new GameObject("EKStat");
					go.transform.SetParent(b.button1.transform, worldPositionStays: false);
					si = go.AddComponent<Image>();
					si.raycastTarget = false;
					RectTransform srt = si.rectTransform;
					srt.anchorMin = new Vector2(1f, 0.5f);
					srt.anchorMax = new Vector2(1f, 0.5f);
					srt.pivot = new Vector2(1f, 0.5f);
				}
				si.enabled = true;
				si.material = null;
				ability.SetImage(si); // base painter = the scaling-stat icon
				if (si.sprite == null)
				{
					si.enabled = false; // no stat sprite (piety...): hide, don't show a white box
					return;
				}
				RectTransform rt = si.rectTransform;
				rt.pivot = new Vector2(1f, 0.5f);
				rt.anchoredPosition = new Vector2(statX, 0f); // right beside the column
				Vector2 s = rt.sizeDelta;
				float max = Mathf.Max(s.x, s.y);
				if (max > 24f || max <= 0f)
				{
					rt.sizeDelta = (max > 0f) ? (s * (24f / max)) : new Vector2(24f, 24f);
				}
			}
			catch
			{
				// cosmetic only
			}
		}

		// scrolling name child for fixedPrefix rows (pooled-row safe)
		private static void SetNameText(ItemGeneral b, string text, float x, float maxWidth)
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
				MenuExtras.ApplyNameDisplay(nt, Mathf.Max(120f, maxWidth));
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
		private static void SetWeightText(ItemGeneral b, string text, float width)
		{
			try
			{
				Transform existing = b.button1.transform.Find("EKWeight");
				UIText wt = (existing != null) ? existing.GetComponent<UIText>() : null;
				if (existing != null && wt == null)
				{
					UnityEngine.Object.Destroy(existing.gameObject); // pre-1.18 plain Text
					existing = null;
				}
				if (wt == null)
				{
					GameObject go = new GameObject("EKWeight");
					go.transform.SetParent(b.button1.transform, worldPositionStays: false);
					wt = go.AddComponent<UIText>();
					wt.font = b.button1.mainText.font;
					wt.fontSize = 15;
					wt.alignment = TextAnchor.MiddleRight;
					wt.color = new Color(0.45f, 0.42f, 0.32f);
					wt.raycastTarget = false;
					RectTransform wrt = wt.rectTransform;
					wrt.anchorMin = new Vector2(1f, 0.5f);
					wrt.anchorMax = new Vector2(1f, 0.5f);
					wrt.pivot = new Vector2(1f, 0.5f);
					wrt.anchoredPosition = new Vector2(-14f, 0f);
				}
				if ((bool)wt)
				{
					wt.rectTransform.sizeDelta = new Vector2(width, 24f);
					wt.text = text ?? "";
					// overly long content scrolls inside the column
					MenuExtras.ApplyNameDisplay(wt, width);
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

		// frame stamp of a row-action dispatch: Close() during dispatch keeps
		// ManualCastMode alive for the action; any OTHER close clears it
		private static int dispatchFrame = -1;

		private static void Run(MenuRow r, Action action)
		{
			if (action == null)
			{
				return;
			}
			dispatchFrame = Time.frameCount;
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
				// ability menus store the category bucket here, item menus weight
				view.Sort((MenuRow a, MenuRow b) => b.sortWeight.CompareTo(a.sortWeight));
			}
			else if (sortMode == 3)
			{
				view.Sort((MenuRow a, MenuRow b) => b.sortCount.CompareTo(a.sortCount));
			}
			// pinned rows ignore the sort: held item on top, utility rows
			// ([ Filter... ], [ Take everything ]...) at the bottom
			List<MenuRow> pinnedTop = null;
			List<MenuRow> pinnedBottom = null;
			foreach (MenuRow rp in view)
			{
				if (rp.pinTop)
				{
					if (pinnedTop == null)
					{
						pinnedTop = new List<MenuRow>();
					}
					pinnedTop.Add(rp);
				}
				else if (rp.pinBottom)
				{
					if (pinnedBottom == null)
					{
						pinnedBottom = new List<MenuRow>();
					}
					pinnedBottom.Add(rp);
				}
			}
			if (pinnedTop != null || pinnedBottom != null)
			{
				view.RemoveAll((MenuRow r2) => r2.pinTop || r2.pinBottom);
				if (pinnedTop != null)
				{
					view.InsertRange(0, pinnedTop);
				}
				if (pinnedBottom != null)
				{
					view.AddRange(pinnedBottom);
				}
			}
			pageRows.Clear();
			// clamp AFTER the view rebuild: Refresh()'s old clamp ran against
			// the stale view, so dropping the last item of the last page left
			// the menu on a page that no longer existed ("None", keys dead)
			page = Mathf.Clamp(page, 0, PageCount - 1);
			int start = page * PageSize;
			for (int i = start; i < view.Count && i < start + PageSize; i++)
			{
				pageRows.Add(view[i]);
			}
			sel = Mathf.Clamp(sel, 0, Mathf.Max(0, pageRows.Count - 1));
			layer.list.List();
			layer.RefreshSize();
			// everything in the CAPTION: the one place that provably renders.
			// Plain ASCII only (no rich text, no fancy glyphs).
			string caption = current.title;
			bool abilityMenu = false;
			foreach (MenuRow r0 in view)
			{
				if (r0.ability != null)
				{
					abilityMenu = true;
					break;
				}
			}
			if (sortMode != 0)
			{
				caption += (sortMode == 1) ? " [name]"
					: ((sortMode == 2) ? (abilityMenu ? " [category]" : " [weight]")
					: (abilityMenu ? " [stock]" : " [count]"));
			}
			if (PageCount > 1)
			{
				caption += "  (" + (page + 1) + "/" + PageCount + ")";
			}
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
				caption += "  " + pieces + " items " + (grams / 1000f).ToString("0.0") + "s";
			}
			// the controls, at the top where text provably renders (plain ASCII)
			caption += abilityMenu ? "  [x aim / w party / z sort]" : "  [z sort / x info / 0 all]";
			layer.windows[0].SetCaption(caption);
			bool colsPage = false;
			bool weightPage = false;
			foreach (MenuRow r1 in pageRows)
			{
				if (r1.cols != null)
				{
					colsPage = true;
					break;
				}
				if (!string.IsNullOrEmpty(r1.rightText))
				{
					weightPage = true;
				}
			}
			RefreshColsHeader(colsPage ? 1 : (weightPage ? 2 : 0));
			RefreshFooter(pieces, grams);
			ShowSelTooltip();
			// remember where the cursor sits, per menu title (Elona habit:
			// reopening Drop/Cast returns to your place)
			cursorMemo[current.title] = (page, sel);
		}

		// Elona-style bottom line, anchored to the LAST row's rendered
		// geometry (same technique as the column header). Two parts, per the
		// user's spec: counts/slots CENTERED, page number bottom-RIGHT, both
		// in the vanilla menu font (copied off the row text).
		private static Text footerCenter;
		private static Text footerPage;

		private static Text MakeFooterText(string name, Text sample, RectTransform winRt, TextAnchor align)
		{
			GameObject go = new GameObject(name);
			go.transform.SetParent(winRt, worldPositionStays: false);
			go.AddComponent<LayoutElement>().ignoreLayout = true; // window layout group must not place it
			Text t = go.AddComponent<Text>();
			t.font = sample.font;
			t.fontSize = Mathf.Max(12, sample.fontSize - 2);
			t.fontStyle = sample.fontStyle;
			t.alignment = align;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.color = new Color(0.38f, 0.35f, 0.27f);
			t.raycastTarget = false;
			return t;
		}

		private static void RefreshFooter(int pieces, int grams)
		{
			try
			{
				if (layer == null || !layer || layer.windows.Count == 0 || layer.list == null || layer.list.buttons.Count == 0)
				{
					return;
				}
				ItemGeneral first = layer.list.buttons[0].component as ItemGeneral;
				ItemGeneral last = layer.list.buttons[layer.list.buttons.Count - 1].component as ItemGeneral;
				if (first == null || !first || last == null || !last || first.button1 == null || first.button1.mainText == null)
				{
					return;
				}
				RectTransform winRt = layer.windows[0].transform as RectTransform;
				Bounds bf = RectTransformUtility.CalculateRelativeRectTransformBounds(winRt, first.button1.transform as RectTransform);
				Bounds bl = RectTransformUtility.CalculateRelativeRectTransformBounds(winRt, last.button1.transform as RectTransform);
				if (footerCenter == null || !footerCenter)
				{
					footerCenter = MakeFooterText("EKFooter", first.button1.mainText, winRt, TextAnchor.UpperCenter);
					footerPage = MakeFooterText("EKFooterPage", first.button1.mainText, winRt, TextAnchor.UpperRight);
					if (Plugin.DebugLog)
					{
						Plugin.Log?.LogInfo("ElonaKeys footer: created from row geometry (bottom=" + bl.min.y + ")");
					}
				}
				float y = bl.min.y - 3f;
				string mid = "";
				if (pieces > 0)
				{
					mid += pieces + " items  " + (grams / 1000f).ToString("0.0") + "s";
				}
				if (current != null && !string.IsNullOrEmpty(current.footerExtra))
				{
					mid += (mid.Length > 0 ? "   " : "") + current.footerExtra;
				}
				footerCenter.text = mid;
				footerCenter.enabled = mid.Length > 0;
				RectTransform rc = footerCenter.rectTransform;
				rc.anchorMin = winRt.pivot;
				rc.anchorMax = winRt.pivot;
				rc.pivot = new Vector2(0.5f, 1f);
				rc.sizeDelta = new Vector2(bf.max.x - bf.min.x, 18f);
				rc.anchoredPosition = new Vector2((bf.min.x + bf.max.x) * 0.5f, y);
				string pg = (PageCount > 1) ? ("page " + (page + 1) + "/" + PageCount) : "";
				footerPage.text = pg;
				footerPage.enabled = pg.Length > 0;
				RectTransform rp = footerPage.rectTransform;
				rp.anchorMin = winRt.pivot;
				rp.anchorMax = winRt.pivot;
				rp.pivot = new Vector2(1f, 1f);
				rp.sizeDelta = new Vector2(160f, 18f);
				rp.anchoredPosition = new Vector2(bf.max.x - 6f, y);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogWarning("ElonaKeys footer failed: " + ex.Message);
			}
		}


		// keep the item stat card visible for the keyboard-selected row
		private static void ShowSelTooltip()
		{
			try
			{
				TooltipManager.Instance.HideTooltips(immediate: true);
				if (sel < 0 || sel >= pageRows.Count || (pageRows[sel].icon == null && pageRows[sel].ability == null && pageRows[sel].note == null) || layer.list.buttons == null || sel >= layer.list.buttons.Count)
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
			if (!Alive)
			{
				if (current != null)
				{
					// the layer died without Close() (Escape kills it directly):
					// run the same state cleanup so nothing sticks
					current = null;
					amountRow = null;
					history.Clear();
					NextFooterExtra = null;
					NextOnTab = null;
					NextOnSwap = null;
					NextRightHeader = null;
					if (Time.frameCount != dispatchFrame)
					{
						Handlers.ManualCastMode = false;
					}
				}
				return;
			}
			if (current == null)
			{
				// alive layer with no screen = an orphan (a failed teardown,
				// e.g. the old distribution-panel bug closing layers mid-flow).
				// While it lingers, PopupNav thinks a menu is open and every
				// native window goes keyboard-dead: self-heal.
				try
				{
					layer.Close();
				}
				catch
				{
				}
				layer = null;
				return;
			}
			// never react to the keystroke that opened this menu
			if (Time.frameCount - openFrame < 2 || EInput.waitReleaseAnyKey)
			{
				return;
			}
			// Unity's EventSystem gives clicked rows FOCUS: Space then "clicks"
			// the focused button (activating rows/spinners invisibly and breaking
			// arrow navigation). No focus, no surprise clicks.
			if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
			{
				UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
			}
			// another window (the examine card, etc.) sits ON TOP of our menu:
			// route Shift/x to it and leave the menu underneath alone.
			// Dialogs (buy-amount popups...) belong to PopupNav: hands off.
			Layer top = (EClass.ui != null) ? EClass.ui.TopLayer : null;
			if (top != null && (object)top != (object)layer)
			{
				if (top is Dialog)
				{
					shiftHeld = false;
					return; // PopupNav navigates it (gate opened for this case)
				}
				if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.X))
				{
					top.Close();
				}
				shiftHeld = false;
				return;
			}
			// per-screen Tab override first (shop: buy <-> sell, the Elona+CGX
			// key; ONLY on family-less menus, family windows keep Tab hopping)
			if (Input.GetKeyDown(KeyCode.Tab) && current.onTab != null)
			{
				current.onTab();
				return;
			}
			// period/comma cycle sources on multi-inventory screens (browser):
			// family-safe, and z/x stay sort/examine
			if (current.onSwap != null)
			{
				if (Input.GetKeyDown(KeyCode.Period))
				{
					current.onSwap(1);
					return;
				}
				if (Input.GetKeyDown(KeyCode.Comma))
				{
					current.onSwap(-1);
					return;
				}
			}
			// Tab cycles the window family (items / equipment-chara / spells-actions).
			// CycledThisFrame: one physical Tab must fire ONE hop — PopupNav
			// also watches Tab and used to bounce the cycle straight back.
			if (Input.GetKeyDown(KeyCode.Tab) && !MenuCycle.CycledThisFrame && MenuCycle.Next())
			{
				return;
			}
			// multi-source screen: z/x flip the shown inventory / category tab.
			// HOLDING x (0.6s) resets to the first tab ([All] / You).
			if (current.sourceCycle != null && !EInput.isCtrlDown && !EInput.isAltDown)
			{
				if (Input.GetKeyDown(KeyCode.Z))
				{
					current.sourceCycle(-1);
					return;
				}
				if (Input.GetKeyDown(KeyCode.X))
				{
					xDownAt = Time.unscaledTime;
					xPending = true;
					return;
				}
				if (xPending && Input.GetKey(KeyCode.X) && Time.unscaledTime - xDownAt >= 0.6f)
				{
					xPending = false;
					current.sourceCycle(0); // hold = reset to the first tab
					return;
				}
				if (xPending && Input.GetKeyUp(KeyCode.X))
				{
					xPending = false;
					current.sourceCycle(1);
					return;
				}
			}
			// x on a castable ability row = aim it manually (free cursor),
			// x elsewhere examines the hovered/selected row
			if (Input.GetKeyDown(KeyCode.X) && !EInput.isCtrlDown && !EInput.isAltDown)
			{
				if (sel >= 0 && sel < pageRows.Count && pageRows[sel].ability != null && pageRows[sel].actSrc != null)
				{
					Handlers.ManualTargetCast(pageRows[sel].ability);
					return;
				}
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
					PageSound();
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
					PageSound();
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
			// numpad0 = act with the WHOLE stack, no spinner (drop all, take
			// all...). Safety rides on the actions themselves: the game's drop
			// refuses undroppables and important-marked items on its own.
			if (Input.GetKeyDown(KeyCode.Keypad0) && sel >= 0 && sel < pageRows.Count)
			{
				MenuRow qr = pageRows[sel];
				if (qr.onAmount != null)
				{
					int all = Mathf.Max(1, qr.maxAmount);
					Run(qr, delegate { qr.onAmount(all); });
					return;
				}
				if (qr.altAct != null)
				{
					Run(qr, qr.altAct);
					return;
				}
			}
			// keypadMultiply = toggle the important mark on the selected item
			// (Elona's * marking; the star refuses drops/sales/shipping)
			if (Input.GetKeyDown(KeyCode.KeypadMultiply) && sel >= 0 && sel < pageRows.Count && pageRows[sel].icon != null)
			{
				Thing mt = pageRows[sel].icon;
				mt.c_isImportant = !mt.c_isImportant;
				Msg.SayRaw(mt.Name + (mt.c_isImportant ? " is marked as important." : " is no longer marked."));
				SE.Tab();
				Refresh();
				if (!Alive || current == null || current.refreshSource == null)
				{
					ApplyPage(); // static menus: at least redraw the star
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
			{
				// Space = Enter, deliberately (it used to fall through to uGUI)
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
				sortMode = (sortMode + 1) % 4;
				sel = 0;
				page = 0;
				ApplyPage();
				return;
			}
			// w = WIDE cast: the selected spell on the whole party (Elona's Wide).
			// Always swallowed here: falling through would move the character.
			if (typed == "w" && !EInput.isShiftDown && !EInput.isCtrlDown && !EInput.isAltDown)
			{
				if (sel >= 0 && sel < pageRows.Count && pageRows[sel].ability != null)
				{
					Handlers.WideCast(pageRows[sel].ability);
				}
				return;
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
			else if (Input.GetKeyDown(KeyCode.Keypad0))
			{
				// numpad0 inside the spinner = confirm with EVERYTHING
				int nAll = Mathf.Max(1, r.maxAmount);
				amountRow = null;
				try
				{
					Run(r, delegate { r.onAmount(nAll); });
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys amount action failed: " + ex);
				}
			}
			else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
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
