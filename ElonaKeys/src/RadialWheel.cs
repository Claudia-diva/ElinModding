using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Classic Elona z-circles: several rings of verbs. Left/right switches ring
	// (remembered between opens), up/down rotates the selection, Enter (or click)
	// activates, z/Escape closes. Input is fenced in InputRouter while open.
	public class RadialWheel : MonoBehaviour
	{
		public class Ring
		{
			public string name;
			public List<(string label, Action act)> items = new List<(string, Action)>();
		}

		private static RadialWheel instance;
		private static int ringIndex; // remembered across opens, like Elona

		public static bool Active => instance != null && instance;

		private List<Ring> rings;
		private readonly List<Text> labels = new List<Text>();
		private Text center;
		private Text hintLeft;
		private Text hintRight;
		private int sel;
		private int openFrame;
		private Font font;

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
			foreach (Text t in labels)
			{
				if ((bool)t)
				{
					UnityEngine.Object.Destroy(t.gameObject);
				}
			}
			labels.Clear();
			if ((bool)center)
			{
				UnityEngine.Object.Destroy(center.gameObject);
			}
			if ((bool)hintLeft)
			{
				UnityEngine.Object.Destroy(hintLeft.gameObject);
			}
			if ((bool)hintRight)
			{
				UnityEngine.Object.Destroy(hintRight.gameObject);
			}
			sel = Mathf.Clamp(sel, 0, Current.items.Count - 1);
			int n = Current.items.Count;
			float radius = Mathf.Min(220f, 100f + n * 10f);
			for (int i = 0; i < n; i++)
			{
				float ang = (90f - (float)i * 360f / n) * Mathf.Deg2Rad;
				Text t = MakeText(transform, font, 20);
				t.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);
				t.text = Current.items[i].label;
				int index = i;
				Button b = t.gameObject.AddComponent<Button>();
				b.onClick.AddListener(delegate
				{
					sel = index;
					Activate();
				});
				labels.Add(t);
			}
			center = MakeText(transform, font, 26);
			center.rectTransform.anchoredPosition = new Vector2(0f, -6f);
			// neighbor-ring links, like Elona's side diamonds
			Ring prev = rings[(ringIndex - 1 + rings.Count) % rings.Count];
			Ring next = rings[(ringIndex + 1) % rings.Count];
			hintLeft = MakeText(transform, font, 17);
			hintLeft.rectTransform.anchoredPosition = new Vector2(-radius - 130f, 0f);
			hintLeft.text = "◄ " + prev.name;
			hintLeft.color = new Color(0.7f, 0.7f, 0.65f);
			Button pb = hintLeft.gameObject.AddComponent<Button>();
			pb.onClick.AddListener(delegate { SwitchRing(-1); });
			hintRight = MakeText(transform, font, 17);
			hintRight.rectTransform.anchoredPosition = new Vector2(radius + 130f, 0f);
			hintRight.text = next.name + " ►";
			hintRight.color = new Color(0.7f, 0.7f, 0.65f);
			Button nb = hintRight.gameObject.AddComponent<Button>();
			nb.onClick.AddListener(delegate { SwitchRing(1); });
			Refresh();
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
				center.text = "- " + Current.name + " -\n<size=20>" + Current.items[sel].label + "</size>";
				center.color = new Color(1f, 0.95f, 0.6f);
			}
		}

		private void SwitchRing(int dir)
		{
			ringIndex = (ringIndex + dir + rings.Count) % rings.Count;
			sel = 0;
			BuildRing();
		}

		private void Activate()
		{
			Action act = Current.items[sel].act;
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
			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				w.SwitchRing(1);
				return;
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow))
			{
				w.SwitchRing(-1);
				return;
			}
			if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				w.sel = (w.sel + 1) % w.Current.items.Count;
				w.Refresh();
				return;
			}
			if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				w.sel = (w.sel - 1 + w.Current.items.Count) % w.Current.items.Count;
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
