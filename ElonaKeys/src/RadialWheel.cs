using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Classic Elona action wheel: verbs arranged in a circle around the center
	// of the screen. Left/up rotates counter-clockwise, right/down clockwise,
	// Enter (or clicking a label) activates, z/Escape closes. Input is fenced in
	// InputRouter while the wheel is up.
	public class RadialWheel : MonoBehaviour
	{
		private static RadialWheel instance;

		public static bool Active => instance != null && instance;

		private readonly List<Text> labels = new List<Text>();
		private List<(string label, Action act)> items;
		private Text center;
		private int sel;
		private int openFrame;

		public static void Toggle(List<(string, Action)> items)
		{
			if (Active)
			{
				CloseWheel();
			}
			else
			{
				Show(items);
			}
		}

		public static void Show(List<(string, Action)> _items)
		{
			CloseWheel();
			if (_items == null || _items.Count == 0 || EClass.ui == null)
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
			instance.items = _items;
			instance.sel = 0;
			instance.openFrame = Time.frameCount;
			instance.Build(root.transform);
			instance.Refresh();
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

		private void Build(Transform root)
		{
			Font font = FindFont();
			float radius = Mathf.Min(240f, 90f + items.Count * 11f);
			for (int i = 0; i < items.Count; i++)
			{
				float ang = (90f - (float)i * 360f / items.Count) * Mathf.Deg2Rad;
				Text t = MakeText(root, font, 20);
				t.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);
				t.text = items[i].label;
				int index = i;
				Button b = t.gameObject.AddComponent<Button>();
				b.onClick.AddListener(delegate
				{
					sel = index;
					Activate();
				});
				labels.Add(t);
			}
			center = MakeText(root, font, 26);
			center.rectTransform.anchoredPosition = Vector2.zero;
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
			t.color = Color.white;
			t.raycastTarget = true;
			t.rectTransform.sizeDelta = new Vector2(180f, 32f);
			Outline o = go.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.9f);
			o.effectDistance = new Vector2(1.5f, -1.5f);
			return t;
		}

		private void Refresh()
		{
			for (int i = 0; i < labels.Count; i++)
			{
				bool on = i == sel;
				labels[i].color = on ? new Color(1f, 0.9f, 0.3f) : new Color(0.85f, 0.85f, 0.8f);
				labels[i].transform.localScale = on ? new Vector3(1.25f, 1.25f, 1f) : Vector3.one;
			}
			if ((bool)center)
			{
				center.text = items[sel].label;
				center.color = new Color(1f, 0.95f, 0.6f);
			}
		}

		private void Activate()
		{
			Action act = items[sel].act;
			CloseWheel();
			try
			{
				act();
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
				return; // ignore the keystroke that opened the wheel
			}
			if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(1))
			{
				CloseWheel();
				return;
			}
			if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
			{
				w.sel = (w.sel + 1) % w.items.Count;
				w.Refresh();
				return;
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
			{
				w.sel = (w.sel - 1 + w.items.Count) % w.items.Count;
				w.Refresh();
				return;
			}
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
			{
				w.Activate();
			}
		}
	}
}
