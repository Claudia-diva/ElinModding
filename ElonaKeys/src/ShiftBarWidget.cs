using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Small persistent overlay showing the NON-displayed quickslot page (the one
	// shift+1..9 fires). Draggable; position saved to config. Refreshes when the
	// toolbelt changes or the page flips.
	public class ShiftBarWidget : MonoBehaviour, IDragHandler, IEndDragHandler
	{
		private static ShiftBarWidget instance;
		private static bool dirty = true;

		private readonly Image[] icons = new Image[9];
		private readonly Image[] bgs = new Image[9];
		private int shownPage = -1;
		private float refreshTimer;

		public static void SetDirty()
		{
			dirty = true;
		}

		// called every frame from MenuExtras.OnUpdate
		public static void EnsureAndUpdate()
		{
			bool wanted = Plugin.ShiftBarVisible && EClass.core != null && EClass.core.IsGameStarted && EClass.pc != null && EClass.ui != null;
			if (!wanted)
			{
				if (instance != null && instance)
				{
					UnityEngine.Object.Destroy(instance.gameObject);
					instance = null;
				}
				return;
			}
			if (instance == null || !instance)
			{
				Build();
			}
			instance.Tick();
		}

		private static void Build()
		{
			GameObject root = new GameObject("ElonaKeysShiftBar");
			root.transform.SetParent(EClass.ui.transform, worldPositionStays: false);
			RectTransform rt = root.AddComponent<RectTransform>();
			rt.sizeDelta = new Vector2(9 * 42f + 8f, 50f);
			rt.anchoredPosition = new Vector2(Plugin.ShiftBarX, Plugin.ShiftBarY);
			Image bg = root.AddComponent<Image>();
			bg.color = new Color(0.05f, 0.04f, 0.03f, 0.45f);
			bg.raycastTarget = true; // drag surface
			instance = root.AddComponent<ShiftBarWidget>();
			Font font = FindFont();
			for (int i = 0; i < 9; i++)
			{
				GameObject slot = new GameObject("slot" + i);
				slot.transform.SetParent(root.transform, worldPositionStays: false);
				RectTransform srt = slot.AddComponent<RectTransform>();
				srt.sizeDelta = new Vector2(40f, 40f);
				srt.anchoredPosition = new Vector2(-((9 - 1) * 42f) / 2f + i * 42f, 2f);
				Image sbg = slot.AddComponent<Image>();
				sbg.color = new Color(0.1f, 0.09f, 0.07f, 0.7f);
				sbg.raycastTarget = false;
				instance.bgs[i] = sbg;
				GameObject icon = new GameObject("icon");
				icon.transform.SetParent(slot.transform, worldPositionStays: false);
				Image img = icon.AddComponent<Image>();
				img.raycastTarget = false;
				img.enabled = false;
				instance.icons[i] = img;
				GameObject lab = new GameObject("num");
				lab.transform.SetParent(slot.transform, worldPositionStays: false);
				Text t = lab.AddComponent<Text>();
				t.font = font;
				t.fontSize = 11;
				t.alignment = TextAnchor.LowerRight;
				t.color = new Color(0.85f, 0.8f, 0.6f, 0.9f);
				t.raycastTarget = false;
				t.text = "S" + (i + 1);
				RectTransform lrt = t.rectTransform;
				lrt.sizeDelta = new Vector2(38f, 14f);
				lrt.anchoredPosition = new Vector2(0f, -14f);
			}
			dirty = true;
		}

		private static Font FindFont()
		{
			Text existing = UnityEngine.Object.FindObjectOfType<Text>();
			if (existing != null && existing.font != null)
			{
				return existing.font;
			}
			return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		}

		private void Tick()
		{
			refreshTimer -= Time.unscaledDeltaTime;
			int otherPage = (EClass.player.hotbarPage == 0) ? 1 : 0;
			if (!dirty && otherPage == shownPage && refreshTimer > 0f && !WidgetCurrentTool.dirty)
			{
				return;
			}
			dirty = false;
			shownPage = otherPage;
			refreshTimer = 1f;
			try
			{
				Thing[] slots = new Thing[9];
				foreach (Thing t in EClass.pc.things)
				{
					if (t.invY == 1 && t.invX >= otherPage * 10 && t.invX < otherPage * 10 + 9)
					{
						slots[t.invX - otherPage * 10] = t;
					}
				}
				for (int i = 0; i < 9; i++)
				{
					Image img = icons[i];
					if (slots[i] == null)
					{
						img.enabled = false;
						continue;
					}
					img.enabled = true;
					slots[i].SetImage(img);
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
						img.material = mat;
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
				}
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
			Plugin.SaveShiftBarPos(rt.anchoredPosition.x, rt.anchoredPosition.y);
		}
	}
}
