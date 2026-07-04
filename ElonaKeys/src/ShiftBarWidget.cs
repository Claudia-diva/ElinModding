using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Overlay showing the NON-displayed quickslot page (what shift+1..9 fires).
	// Built by cloning the live hotbar widget (WidgetCurrentTool), so size, skin
	// and slot rendering are pixel-identical to the real bar; the clone's UIList
	// is re-driven with the other page's things through the same InvOwnerHotbar,
	// which also makes drag-assign onto the bar work. Draggable by its empty
	// space; offset from the hotbar saved to config.
	public class ShiftBarWidget : MonoBehaviour, IDragHandler, IEndDragHandler
	{
		private static ShiftBarWidget instance;
		private static bool dirty = true;

		private UIList list;
		private int shownPage = -1;
		private float refreshTimer;
		private Vector2 cellAtBuild;

		public static void SetDirty()
		{
			dirty = true;
		}

		// called every frame from MenuExtras.OnUpdate
		public static void EnsureAndUpdate()
		{
			WidgetCurrentTool src = WidgetCurrentTool.Instance;
			bool wanted = Plugin.ShiftBarVisible && EClass.core != null && EClass.core.IsGameStarted
				&& EClass.pc != null && EClass.ui != null && src != null && (bool)src;
			if (!wanted)
			{
				Drop();
				return;
			}
			// follow the hotbar's icon-size setting: rebuild when it changes
			if (instance != null && instance && instance.cellAtBuild != CellSize(src))
			{
				Drop();
			}
			if (instance == null || !instance)
			{
				try
				{
					Build(src);
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys shift bar build failed: " + ex);
					return;
				}
			}
			if (instance != null && instance)
			{
				instance.Tick();
			}
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

		private static void Drop()
		{
			if (instance != null && instance)
			{
				UnityEngine.Object.Destroy(instance.gameObject);
			}
			instance = null;
		}

		private static int OtherPage()
		{
			return (EClass.player.hotbarPage == 0) ? 1 : 0;
		}

		private static void Build(WidgetCurrentTool src)
		{
			GameObject root = UnityEngine.Object.Instantiate(src.gameObject, src.transform.parent);
			root.name = "ElonaKeysShiftBar";
			WidgetCurrentTool comp = root.GetComponent<WidgetCurrentTool>();
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
			// strip everything that isn't the slot grid
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
			// the widget component's Update would NRE on the stripped pieces
			UnityEngine.Object.DestroyImmediate(comp);
			foreach (UIPlaceHelper ph in root.GetComponentsInChildren<UIPlaceHelper>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(ph);
			}
			instance = root.AddComponent<ShiftBarWidget>();
			instance.list = clonedList;
			instance.cellAtBuild = CellSize(src);
			layout.cellSize = instance.cellAtBuild;
			RectTransform rt = root.transform as RectTransform;
			RectTransform srcRt = src.transform as RectTransform;
			rt.anchoredPosition = srcRt.anchoredPosition + new Vector2(Plugin.ShiftBarX, Plugin.ShiftBarY);
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

		private void Tick()
		{
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

		public void OnDrag(PointerEventData e)
		{
			RectTransform rt = transform as RectTransform;
			rt.anchoredPosition += e.delta;
		}

		public void OnEndDrag(PointerEventData e)
		{
			RectTransform rt = transform as RectTransform;
			WidgetCurrentTool src = WidgetCurrentTool.Instance;
			if (src != null && (bool)src)
			{
				Vector2 off = rt.anchoredPosition - (src.transform as RectTransform).anchoredPosition;
				Plugin.SaveShiftBarPos(off.x, off.y);
			}
		}
	}
}
