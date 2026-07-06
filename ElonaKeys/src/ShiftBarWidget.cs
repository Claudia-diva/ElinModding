using System;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// The OTHER quickslot page (what shift+1..9 fires), shown as small corner
	// icons ON the real hotbar's slots instead of a second bar: always aligned,
	// always the right skin, zero layout drift. Driven from MenuExtras.OnUpdate.
	public static class ShiftOverlay
	{
		private const string IconName = "EKShiftIcon";

		private static bool dirty = true;
		private static bool lastVisible = true;
		private static int shownPage = -1;
		private static float refreshTimer;

		public static void SetDirty()
		{
			dirty = true;
		}

		public static void OnUpdate()
		{
			WidgetCurrentTool w = WidgetCurrentTool.Instance;
			if (w == null || !w || EClass.pc == null)
			{
				return;
			}
			if (Plugin.ShiftBarVisible != lastVisible)
			{
				lastVisible = Plugin.ShiftBarVisible;
				dirty = true;
			}
			if (!Plugin.ShiftBarVisible)
			{
				if (dirty)
				{
					dirty = false;
					RemoveAll(w);
				}
				return;
			}
			refreshTimer -= Time.unscaledDeltaTime;
			int other = (EClass.player.hotbarPage == 0) ? 1 : 0;
			if (!dirty && other == shownPage && refreshTimer > 0f)
			{
				return;
			}
			dirty = false;
			shownPage = other;
			refreshTimer = 0.5f;
			try
			{
				Refresh(w, other);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys shift overlay failed: " + ex);
			}
		}

		private static void Refresh(WidgetCurrentTool w, int other)
		{
			// other-page things by slot
			Thing[] grid = new Thing[10];
			foreach (Thing t in EClass.pc.things)
			{
				if (t.invY == 1 && t.invX >= other * 10 && t.invX < other * 10 + 10)
				{
					grid[t.invX - other * 10] = t;
				}
			}
			for (int i = 0; i < w.list.buttons.Count && i < 9; i++)
			{
				Component comp = w.list.buttons[i].component;
				if (comp == null || !comp)
				{
					continue;
				}
				Transform slot = comp.transform;
				Transform ovt = slot.Find(IconName);
				Image icon;
				Image backing;
				if (ovt == null)
				{
					GameObject bg = new GameObject(IconName);
					bg.transform.SetParent(slot, worldPositionStays: false);
					backing = bg.AddComponent<Image>();
					backing.color = new Color(0.05f, 0.04f, 0.03f, 0.55f);
					backing.raycastTarget = false;
					RectTransform brt = backing.rectTransform;
					brt.anchorMin = new Vector2(1f, 1f);
					brt.anchorMax = new Vector2(1f, 1f);
					brt.pivot = new Vector2(1f, 1f);
					brt.anchoredPosition = new Vector2(-1f, -1f);
					brt.sizeDelta = new Vector2(24f, 24f);
					GameObject ig = new GameObject("icon");
					ig.transform.SetParent(bg.transform, worldPositionStays: false);
					icon = ig.AddComponent<Image>();
					icon.raycastTarget = false;
				}
				else
				{
					backing = ovt.GetComponent<Image>();
					icon = ovt.Find("icon").GetComponent<Image>();
				}
				Thing thing = grid[i];
				if (thing == null)
				{
					backing.enabled = false;
					icon.enabled = false;
					continue;
				}
				backing.enabled = true;
				icon.enabled = true;
				thing.SetImage(icon);
				Material mat = null;
				try
				{
					mat = EClass.core.refs.matUIObj;
				}
				catch
				{
				}
				if (mat != null)
				{
					icon.material = mat;
				}
				else
				{
					icon.color = Color.white;
				}
				RectTransform rt = icon.rectTransform;
				rt.pivot = new Vector2(0.5f, 0.5f);
				rt.anchorMin = new Vector2(0.5f, 0.5f);
				rt.anchorMax = new Vector2(0.5f, 0.5f);
				rt.anchoredPosition = Vector2.zero;
				icon.transform.localScale = new Vector3(Mathf.Abs(icon.transform.localScale.x), icon.transform.localScale.y, 1f);
				Vector2 s = rt.sizeDelta;
				float max = Mathf.Max(s.x, s.y);
				if (max > 20f)
				{
					rt.sizeDelta = s * (20f / max);
				}
			}
		}

		private static void RemoveAll(WidgetCurrentTool w)
		{
			for (int i = 0; i < w.list.buttons.Count; i++)
			{
				Component comp = w.list.buttons[i].component;
				if (comp != null && (bool)comp)
				{
					Transform ovt = comp.transform.Find(IconName);
					if (ovt != null)
					{
						UnityEngine.Object.Destroy(ovt.gameObject);
					}
				}
			}
		}
	}
}
