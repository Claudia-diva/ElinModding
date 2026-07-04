using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Classic Elona z-circles, faithful edition: 8 compass slots per ring,
	// pressing a DIRECTION instantly triggers that slot. East/west slots are the
	// links to the neighboring rings (so "right" walks you through the circles,
	// and the current ring is remembered between opens). z/Escape/right-click
	// closes; clicking a diamond also works.
	public class RadialWheel : MonoBehaviour
	{
		public class Ring
		{
			public string name;
			public List<(string label, Action act)> items = new List<(string, Action)>();
		}

		private class Slot
		{
			public string label;
			public Action act;      // verb, or null
			public int link;        // -1 prev ring, +1 next ring, 0 none
			public Text text;
		}

		private static RadialWheel instance;
		private static int ringIndex; // remembered across opens, like Elona

		public static bool Active => instance != null && instance;

		// compass order: N, NE, E, SE, S, SW, W, NW
		private static readonly Vector2[] dirs =
		{
			new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(1f, -1f),
			new Vector2(0f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 0f), new Vector2(-1f, 1f)
		};
		private const int SlotE = 2;
		private const int SlotW = 6;
		private static readonly int[] verbSlots = { 0, 1, 3, 4, 5, 7 }; // N, NE, SE, S, SW, NW

		private List<Ring> rings;
		private readonly Slot[] slots = new Slot[8];
		private Text center;
		private int openFrame;
		private Font font;
		private Vector2 prevAxis;

		public static void Toggle(List<Ring> rings)
		{
			if (Active)
			{
				CloseWheel();
			}
			else
			{
				Show(rings);
			}
		}

		public static void Show(List<Ring> _rings)
		{
			CloseWheel();
			if (_rings == null || _rings.Count == 0 || EClass.ui == null)
			{
				return;
			}
			GameObject root = new GameObject("ElonaKeysWheel");
			root.transform.SetParent(EClass.ui.transform, worldPositionStays: false);
			RectTransform rt = root.AddComponent<RectTransform>();
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			Image bg = root.AddComponent<Image>();
			bg.color = new Color(0f, 0f, 0f, 0.45f);
			bg.raycastTarget = true;
			instance = root.AddComponent<RadialWheel>();
			instance.rings = _rings;
			instance.font = FindFont();
			instance.openFrame = Time.frameCount;
			instance.prevAxis = Vector2.one; // require release before first flick
			ringIndex = Mathf.Clamp(ringIndex, 0, _rings.Count - 1);
			instance.BuildRing();
		}

		public static void CloseWheel()
		{
			if (instance != null && instance)
			{
				UnityEngine.Object.Destroy(instance.gameObject);
			}
			instance = null;
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

		private Ring Current => rings[ringIndex];

		private void BuildRing()
		{
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i] != null && (bool)slots[i].text)
				{
					UnityEngine.Object.Destroy(slots[i].text.gameObject);
				}
				slots[i] = null;
			}
			if ((bool)center)
			{
				UnityEngine.Object.Destroy(center.gameObject);
			}
			// ring links east/west (linear chain, like Elona)
			if (ringIndex + 1 < rings.Count)
			{
				slots[SlotE] = new Slot { label = rings[ringIndex + 1].name + " ►", link = 1 };
			}
			if (ringIndex - 1 >= 0)
			{
				slots[SlotW] = new Slot { label = "◄ " + rings[ringIndex - 1].name, link = -1 };
			}
			// verbs on the remaining compass points
			for (int v = 0; v < Current.items.Count && v < verbSlots.Length; v++)
			{
				(string label, Action act) item = Current.items[v];
				slots[verbSlots[v]] = new Slot { label = item.label, act = item.act };
			}
			// render
			for (int i = 0; i < slots.Length; i++)
			{
				Slot s = slots[i];
				if (s == null)
				{
					continue;
				}
				Text t = MakeText(transform, font, (s.link != 0) ? 18 : 21);
				float rx = 190f;
				float ry = 120f;
				t.rectTransform.anchoredPosition = new Vector2(dirs[i].x * rx, dirs[i].y * ry);
				t.text = s.label;
				t.color = (s.link != 0) ? new Color(0.72f, 0.72f, 0.66f) : new Color(0.95f, 0.93f, 0.85f);
				int index = i;
				Button b = t.gameObject.AddComponent<Button>();
				b.onClick.AddListener(delegate { Trigger(index); });
				s.text = t;
			}
			center = MakeText(transform, font, 26);
			center.rectTransform.anchoredPosition = Vector2.zero;
			center.text = "- " + Current.name + " -";
			center.color = new Color(1f, 0.95f, 0.6f);
		}

		private static Text MakeText(Transform root, Font font, int size)
		{
			GameObject go = new GameObject("label");
			go.transform.SetParent(root, worldPositionStays: false);
			Text t = go.AddComponent<Text>();
			t.font = font;
			t.fontSize = size;
			t.alignment = TextAnchor.MiddleCenter;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.verticalOverflow = VerticalWrapMode.Overflow;
			t.raycastTarget = true;
			t.rectTransform.sizeDelta = new Vector2(170f, 32f);
			Outline o = go.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.9f);
			o.effectDistance = new Vector2(1.5f, -1.5f);
			return t;
		}

		private void Trigger(int slotIndex)
		{
			Slot s = slots[slotIndex];
			if (s == null)
			{
				return;
			}
			if (s.link != 0)
			{
				ringIndex = Mathf.Clamp(ringIndex + s.link, 0, rings.Count - 1);
				BuildRing();
				return;
			}
			Action act = s.act;
			CloseWheel();
			try
			{
				act?.Invoke();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys wheel action failed: " + ex);
			}
		}

		// called from InputRouter every frame while active
		public static void HandleKeys()
		{
			RadialWheel w = instance;
			if (w == null || !w)
			{
				return;
			}
			if (Time.frameCount - w.openFrame < 2)
			{
				w.prevAxis = Vector2.one;
				return; // ignore the keystroke that opened the wheel
			}
			if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(1))
			{
				CloseWheel();
				return;
			}
			// instant 8-way selection on a fresh direction press
			Vector2 a = EInput.axis;
			if (a != Vector2.zero && w.prevAxis == Vector2.zero)
			{
				int sx = Math.Sign(a.x);
				int sy = Math.Sign(a.y);
				for (int i = 0; i < dirs.Length; i++)
				{
					if ((int)dirs[i].x == sx && (int)dirs[i].y == sy)
					{
						w.prevAxis = a;
						w.Trigger(i);
						return;
					}
				}
			}
			w.prevAxis = a;
		}
	}
}
