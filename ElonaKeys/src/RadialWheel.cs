using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Classic Elona z-circles: 8 diamond slots per ring, pressing a DIRECTION
	// instantly triggers that slot. East/west diamonds link to the neighboring
	// rings; the current ring is remembered between opens. The whole wheel is a
	// draggable widget (drag the center diamond), position saved to config.
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
			public Action act;
			public int link;
			public GameObject go;
		}

		// drag handle on the center diamond
		private class DragHandle : MonoBehaviour, IDragHandler, IEndDragHandler
		{
			public RectTransform target;

			public void OnDrag(PointerEventData e)
			{
				if ((bool)target)
				{
					target.anchoredPosition += e.delta;
				}
			}

			public void OnEndDrag(PointerEventData e)
			{
				if ((bool)target)
				{
					Plugin.SaveWheelPos(target.anchoredPosition.x, target.anchoredPosition.y);
				}
			}
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
		private RectTransform container;
		private GameObject centerGo;
		private int openFrame;
		private Font font;
		private Vector2 pending;   // last held direction; fires on release
		private bool armed;        // axis must be seen at zero once after opening
		private int highlighted = -1;

		private static readonly Color BorderNormal = new Color(0.7f, 0.65f, 0.55f, 0.7f);
		private static readonly Color BorderPending = new Color(1f, 0.9f, 0.45f, 0.95f);

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
			bg.color = new Color(0f, 0f, 0f, 0.4f);
			bg.raycastTarget = true;
			instance = root.AddComponent<RadialWheel>();
			instance.rings = _rings;
			instance.font = FindFont();
			instance.openFrame = Time.frameCount;
			instance.armed = false; // ignore whatever is held while opening
			GameObject cont = new GameObject("container");
			cont.transform.SetParent(root.transform, worldPositionStays: false);
			instance.container = cont.AddComponent<RectTransform>();
			instance.container.anchoredPosition = new Vector2(Plugin.WheelPosX, Plugin.WheelPosY);
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
			highlighted = -1;
			pending = Vector2.zero;
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i] != null && (bool)slots[i].go)
				{
					UnityEngine.Object.Destroy(slots[i].go);
				}
				slots[i] = null;
			}
			if ((bool)centerGo)
			{
				UnityEngine.Object.Destroy(centerGo);
			}
			if (ringIndex + 1 < rings.Count)
			{
				slots[SlotE] = new Slot { label = rings[ringIndex + 1].name + " ►", link = 1 };
			}
			if (ringIndex - 1 >= 0)
			{
				slots[SlotW] = new Slot { label = "◄ " + rings[ringIndex - 1].name, link = -1 };
			}
			for (int v = 0; v < Current.items.Count && v < verbSlots.Length; v++)
			{
				(string label, Action act) item = Current.items[v];
				slots[verbSlots[v]] = new Slot { label = item.label, act = item.act };
			}
			float rx = 200f;
			float ry = 128f;
			for (int i = 0; i < slots.Length; i++)
			{
				Slot s = slots[i];
				if (s == null)
				{
					continue;
				}
				bool isLink = s.link != 0;
				s.go = MakeDiamond(container, font, s.label, isLink, new Vector2(dirs[i].x * rx, dirs[i].y * ry));
				int index = i;
				s.go.GetComponent<Button>().onClick.AddListener(delegate { Trigger(index); });
			}
			// center diamond: ring name, doubles as the drag handle
			centerGo = MakeDiamond(container, font, Current.name, isLink: false, Vector2.zero, center: true);
			DragHandle dh = centerGo.AddComponent<DragHandle>();
			dh.target = container;
		}

		private GameObject MakeDiamond(Transform parent, Font f, string label, bool isLink, Vector2 pos, bool center = false)
		{
			GameObject go = new GameObject("slot");
			go.transform.SetParent(parent, worldPositionStays: false);
			RectTransform rt = go.AddComponent<RectTransform>();
			rt.anchoredPosition = pos;
			rt.sizeDelta = new Vector2(96f, 96f);
			// invisible raycast surface so clicks land on the whole slot
			Image hit = go.AddComponent<Image>();
			hit.color = new Color(0f, 0f, 0f, 0f);
			hit.raycastTarget = true;
			go.AddComponent<Button>();
			// diamond = mostly transparent fill with a simple border: an outer
			// rotated square (border color) with a slightly smaller dark inner
			GameObject dia = new GameObject("diamond");
			dia.transform.SetParent(go.transform, worldPositionStays: false);
			Image border = dia.AddComponent<Image>();
			border.raycastTarget = false;
			border.color = center ? new Color(0.85f, 0.75f, 0.5f, 0.85f) : new Color(0.7f, 0.65f, 0.55f, 0.7f);
			RectTransform drt = border.rectTransform;
			drt.sizeDelta = new Vector2(72f, 72f);
			drt.localRotation = Quaternion.Euler(0f, 0f, 45f);
			GameObject inner = new GameObject("fill");
			inner.transform.SetParent(dia.transform, worldPositionStays: false);
			Image fill = inner.AddComponent<Image>();
			fill.raycastTarget = false;
			fill.color = new Color(0.05f, 0.04f, 0.03f, 0.35f);
			fill.rectTransform.sizeDelta = new Vector2(66f, 66f);
			// label on top, not rotated
			GameObject lg = new GameObject("label");
			lg.transform.SetParent(go.transform, worldPositionStays: false);
			Text t = lg.AddComponent<Text>();
			t.font = f;
			t.fontSize = center ? 20 : (isLink ? 15 : 17);
			t.alignment = TextAnchor.MiddleCenter;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.verticalOverflow = VerticalWrapMode.Overflow;
			t.color = center ? new Color(1f, 0.95f, 0.6f) : (isLink ? new Color(0.78f, 0.78f, 0.72f) : new Color(0.97f, 0.95f, 0.88f));
			t.raycastTarget = false;
			t.rectTransform.sizeDelta = new Vector2(160f, 32f);
			t.text = label; // 1.8.0 forgot this: diamonds rendered with no text
			Outline o = lg.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.9f);
			o.effectDistance = new Vector2(1.5f, -1.5f);
			return go;
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

		private static int SlotFromAxis(Vector2 a)
		{
			int sx = Math.Sign(a.x);
			int sy = Math.Sign(a.y);
			for (int i = 0; i < dirs.Length; i++)
			{
				if ((int)dirs[i].x == sx && (int)dirs[i].y == sy)
				{
					return i;
				}
			}
			return -1;
		}

		// gold border + slight scale on the slot that will fire on release
		private void SetPendingHighlight(int idx)
		{
			if (highlighted == idx)
			{
				return;
			}
			highlighted = idx;
			for (int i = 0; i < slots.Length; i++)
			{
				Slot s = slots[i];
				if (s == null || !s.go)
				{
					continue;
				}
				bool on = i == idx;
				Transform dia = s.go.transform.Find("diamond");
				if (dia != null)
				{
					Image border = dia.GetComponent<Image>();
					if ((bool)border)
					{
						border.color = on ? BorderPending : BorderNormal;
					}
				}
				s.go.transform.localScale = on ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
			}
		}

		// called from InputRouter every frame while active. Direction confirms
		// on RELEASE: holding up then adding right composes to NE; nothing
		// fires while keys are still going down (mispresses are correctable).
		public static void HandleKeys()
		{
			RadialWheel w = instance;
			if (w == null || !w)
			{
				return;
			}
			if (Time.frameCount - w.openFrame < 2)
			{
				return; // ignore the keystroke that opened the wheel
			}
			if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(1))
			{
				CloseWheel();
				return;
			}
			Vector2 a = EInput.axis;
			if (!w.armed)
			{
				if (a == Vector2.zero)
				{
					w.armed = true;
				}
				return;
			}
			if (a != Vector2.zero)
			{
				w.pending = a;
				w.SetPendingHighlight(SlotFromAxis(a));
				return;
			}
			if (w.pending != Vector2.zero)
			{
				Vector2 p = w.pending;
				w.pending = Vector2.zero;
				int i = SlotFromAxis(p);
				if (i != -1)
				{
					w.Trigger(i);
				}
			}
		}
	}
}
