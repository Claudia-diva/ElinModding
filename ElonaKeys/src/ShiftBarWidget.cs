using System;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// The other quickslot page (what shift+1..9 fires) as a REAL Elin widget:
	// registered via WidgetHost, so toggle/drag/lock/anchor/theme-save are all
	// native. The visible grid is a clone of the live hotbar widget
	// (WidgetCurrentTool) re-driven with the non-displayed page, which keeps
	// the exact size/skin and makes drag-assign onto the slots work.
	public class WidgetEKShiftBar : Widget
	{
		private static bool dirty = true;

		private UIList list;
		private GameObject gridRoot;
		private int shownPage = -1;
		private float refreshTimer;
		private Vector2 cellAtBuild;

		public override bool ShowStyleMenu => false; // no SkinRoot on runtime widgets

		public static void SetDirty()
		{
			dirty = true;
		}

		public override void OnActivate()
		{
			TryBuildGrid();
		}

		private static Vector2 CellSize(WidgetCurrentTool src)
		{
			try
			{
				return EClass.setting.ui.iconSizes[src.extra.iconSize];
			}
			catch
			{
				return new Vector2(40f, 40f);
			}
		}

		private static int OtherPage()
		{
			return (EClass.player.hotbarPage == 0) ? 1 : 0;
		}

		private void TryBuildGrid()
		{
			WidgetCurrentTool src = WidgetCurrentTool.Instance;
			if (src == null || !src)
			{
				return; // hotbar widget not up yet; Update retries
			}
			if (gridRoot != null)
			{
				UnityEngine.Object.Destroy(gridRoot);
				gridRoot = null;
				list = null;
			}
			GameObject clone = UnityEngine.Object.Instantiate(src.gameObject, base.transform);
			clone.name = "grid";
			WidgetCurrentTool comp = clone.GetComponent<WidgetCurrentTool>();
			UIList clonedList = comp.list;
			GridLayoutGroup layout = comp.layout;
			int slots = Mathf.Clamp(src.maxSlots, 1, 9);
			bool showShortcut = false;
			try
			{
				showShortcut = src.extra.showShortcut;
			}
			catch
			{
			}
			if ((bool)comp.buttonHotItem)
			{
				UnityEngine.Object.DestroyImmediate(comp.buttonHotItem.gameObject);
			}
			if ((bool)comp.buttonSwitchPage)
			{
				UnityEngine.Object.DestroyImmediate(comp.buttonSwitchPage.gameObject);
			}
			if ((bool)comp.iconHeld)
			{
				UnityEngine.Object.DestroyImmediate(comp.iconHeld.gameObject);
			}
			if ((bool)comp.imageHighlight)
			{
				UnityEngine.Object.DestroyImmediate(comp.imageHighlight.gameObject);
			}
			if ((bool)comp.transHighlightSwitch)
			{
				UnityEngine.Object.DestroyImmediate(comp.transHighlightSwitch.gameObject);
			}
			// the cloned widget component's Update would NRE on stripped pieces
			UnityEngine.Object.DestroyImmediate(comp);
			foreach (UIDragPanel dp in clone.GetComponentsInChildren<UIDragPanel>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(dp); // ours on the root handles dragging
			}
			foreach (UIPlaceHelper ph in clone.GetComponentsInChildren<UIPlaceHelper>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(ph);
			}
			gridRoot = clone;
			list = clonedList;
			cellAtBuild = CellSize(src);
			layout.cellSize = cellAtBuild;
			// center the clone in the widget; size the widget to fit the row
			RectTransform crt = clone.transform as RectTransform;
			crt.anchorMin = new Vector2(0.5f, 0.5f);
			crt.anchorMax = new Vector2(0.5f, 0.5f);
			crt.anchoredPosition = Vector2.zero;
			RectTransform rt = base.transform as RectTransform;
			rt.sizeDelta = new Vector2(slots * (cellAtBuild.x + 2f) + 12f, cellAtBuild.y + 12f);
			// same wiring as WidgetCurrentTool.RebuildSlots, aimed at the other page
			clonedList.Clear();
			clonedList.callbacks = new UIList.Callback<Thing, ButtonGridDrag>
			{
				onRedraw = delegate(Thing a, ButtonGridDrag b, int i)
				{
					int other = OtherPage();
					b.subText2.SetText(((i + 1) % 10).ToString() ?? "");
					b.subText2.SetActive(showShortcut);
					b.index = i % 10 + 10 * other;
					b.SetCardGrid(a, new InvOwnerHotbar(EClass.pc)
					{
						index = i % 10 + 10 * other
					});
				}
			};
			for (int j = 0; j < slots; j++)
			{
				clonedList.Add(null);
			}
			clonedList.onBeforeRedraw = delegate
			{
				int other = OtherPage();
				Thing[] grid = new Thing[20];
				foreach (Thing t in EClass.pc.things)
				{
					if (t.invY == 1 && t.invX >= other * 10 && t.invX < other * 10 + 10 && t.invX < 20)
					{
						grid[t.invX] = t;
					}
				}
				for (int l = 0; l < clonedList.buttons.Count; l++)
				{
					UIList.ButtonPair pair = clonedList.buttons[l];
					pair.obj = grid[l + other * 10];
					clonedList.buttons[l] = pair;
				}
			};
			clonedList.onAfterRedraw = null;
			clonedList.Refresh();
			clonedList.Redraw();
			dirty = true;
		}

		private void Update()
		{
			WidgetCurrentTool src = WidgetCurrentTool.Instance;
			bool haveSrc = src != null && (bool)src;
			if (gridRoot == null || list == null)
			{
				if (haveSrc)
				{
					TryBuildGrid();
				}
				return;
			}
			if (haveSrc && cellAtBuild != CellSize(src))
			{
				TryBuildGrid(); // hotbar icon size changed: match it
				return;
			}
			refreshTimer -= Time.unscaledDeltaTime;
			int other = OtherPage();
			if (!dirty && other == shownPage && refreshTimer > 0f && !WidgetCurrentTool.dirty)
			{
				return;
			}
			dirty = false;
			shownPage = other;
			refreshTimer = 1f;
			try
			{
				list.Redraw();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys shift bar refresh failed: " + ex);
			}
		}
	}
}
