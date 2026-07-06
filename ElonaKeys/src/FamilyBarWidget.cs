using System;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Elona's top window-family bar as a REAL Elin widget (WidgetHost registers
	// it): a row of diamonds for the current family (items / equipment-chara /
	// spells-actions), the open window highlighted gold, key letters beneath,
	// "Tab [change]" hint. Clicking a diamond jumps to that window. Only shown
	// while a family window is open (always shown in widget-edit mode so it
	// can be dragged). Position/toggle/lock live in the native widget system.
	public class WidgetEKFamilyBar : Widget
	{
		private const float Spacing = 92f;

		private string builtGroup;
		private int builtIndex = -1;
		private GameObject content;
		private Font font;

		public override bool ShowStyleMenu => false; // no SkinRoot on runtime widgets

		public override void OnActivate()
		{
			font = RadialWheel.FindFont();
			// fresh config sits at screen center: park the bar near the top
			RectTransform rt = base.transform as RectTransform;
			if (config.x == 0f && config.y == 0f)
			{
				rt.anchoredPosition = new Vector2(0f, (float)Screen.height * 0.5f - 90f);
			}
		}

		private void Update()
		{
			string g = null;
			int idx = -1;
			if (MenuSystem.Alive && MenuCycle.Group != null)
			{
				g = MenuCycle.Group;
				idx = MenuCycle.Index;
			}
			else if (EClass.ui != null && EClass.ui.TopLayer is LayerChara && MenuCycle.Group == "chara")
			{
				g = "chara";
				idx = MenuCycle.Index;
			}
			if (g == null && (bool)LayerWidget.Instance)
			{
				g = "items"; // widget-edit mode: show a sample so it can be placed
				idx = 0;
			}
			if (g == null)
			{
				if (content != null)
				{
					content.SetActive(value: false);
				}
				builtGroup = null;
				return;
			}
			if (content != null && g == builtGroup && idx == builtIndex)
			{
				content.SetActive(value: true);
				return;
			}
			Rebuild(g, idx);
		}

		private void Rebuild(string g, int idx)
		{
			builtGroup = g;
			builtIndex = idx;
			if (content != null)
			{
				UnityEngine.Object.Destroy(content);
			}
			(string name, string key, Action open)[] entries = MenuCycle.Entries(g);
			if (entries == null)
			{
				return;
			}
			content = new GameObject("content");
			content.transform.SetParent(base.transform, worldPositionStays: false);
			RectTransform crt = content.AddComponent<RectTransform>();
			crt.anchorMin = new Vector2(0.5f, 0.5f);
			crt.anchorMax = new Vector2(0.5f, 0.5f);
			crt.anchoredPosition = Vector2.zero;
			float width = entries.Length * Spacing + 40f;
			(base.transform as RectTransform).sizeDelta = new Vector2(width, 120f);
			BuildBg(crt, width);
			float x0 = (0f - (entries.Length - 1)) * Spacing / 2f;
			for (int i = 0; i < entries.Length; i++)
			{
				(string name, string key, Action open) e = entries[i];
				Vector2 pos = new Vector2(x0 + i * Spacing, 12f);
				GameObject dia = RadialWheel.MakeDiamond(content.transform, font, e.name, isLink: false, pos);
				if (i == idx)
				{
					Transform d = dia.transform.Find("diamond");
					Image border = (d != null) ? d.GetComponent<Image>() : null;
					if ((bool)border)
					{
						border.color = new Color(1f, 0.9f, 0.45f, 0.95f);
					}
					dia.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
				}
				// key letter under the diamond, Elona style
				GameObject kg = new GameObject("key");
				kg.transform.SetParent(content.transform, worldPositionStays: false);
				Text kt = kg.AddComponent<Text>();
				kt.font = font;
				kt.fontSize = 14;
				kt.alignment = TextAnchor.MiddleCenter;
				kt.color = (i == idx) ? new Color(1f, 0.9f, 0.45f) : new Color(0.85f, 0.83f, 0.75f);
				kt.raycastTarget = false;
				kt.text = "(" + e.key + ")";
				RectTransform krt = kt.rectTransform;
				krt.anchoredPosition = new Vector2(pos.x, pos.y - 46f);
				krt.sizeDelta = new Vector2(60f, 18f);
				AddOutline(kg);
				int ci = i;
				dia.GetComponent<Button>().onClick.AddListener(delegate
				{
					Jump(ci);
				});
			}
			// hint line, like Elona's ",Tab [Change]"
			GameObject hg = new GameObject("hint");
			hg.transform.SetParent(content.transform, worldPositionStays: false);
			Text ht = hg.AddComponent<Text>();
			ht.font = font;
			ht.fontSize = 13;
			ht.alignment = TextAnchor.MiddleCenter;
			ht.color = new Color(1f, 0.55f, 0.35f);
			ht.raycastTarget = false;
			ht.text = "Tab [change]";
			RectTransform hrt = ht.rectTransform;
			hrt.anchoredPosition = new Vector2(0f, -58f);
			hrt.sizeDelta = new Vector2(220f, 18f);
			AddOutline(hg);
		}

		private static void AddOutline(GameObject go)
		{
			Outline o = go.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.9f);
			o.effectDistance = new Vector2(1.2f, -1.2f);
		}

		// native-skin strip borrowed from the hotbar's background
		private void BuildBg(RectTransform parent, float width)
		{
			try
			{
				WidgetCurrentTool src = WidgetCurrentTool.Instance;
				UIRawImage bg = (src != null && (bool)src) ? src.GetComponentInChildren<UIRawImage>(includeInactive: true) : null;
				if (bg != null)
				{
					GameObject copy = UnityEngine.Object.Instantiate(bg.gameObject, parent);
					copy.name = "bg";
					RawImage ri = copy.GetComponent<RawImage>();
					ri.raycastTarget = false;
					RectTransform r = copy.transform as RectTransform;
					r.anchorMin = new Vector2(0.5f, 0.5f);
					r.anchorMax = new Vector2(0.5f, 0.5f);
					r.anchoredPosition = new Vector2(0f, 8f);
					r.sizeDelta = new Vector2(width, 78f);
					copy.transform.SetAsFirstSibling();
					return;
				}
			}
			catch
			{
			}
			GameObject fb = new GameObject("bg");
			fb.transform.SetParent(parent, worldPositionStays: false);
			Image img = fb.AddComponent<Image>();
			img.color = new Color(0.05f, 0.04f, 0.03f, 0.45f);
			img.raycastTarget = false;
			RectTransform frt = img.rectTransform;
			frt.anchoredPosition = new Vector2(0f, 8f);
			frt.sizeDelta = new Vector2(width, 78f);
			fb.transform.SetAsFirstSibling();
		}

		private void Jump(int i)
		{
			if ((bool)LayerWidget.Instance)
			{
				return; // widget-edit mode: the sample is for placing, not clicking
			}
			(string name, string key, Action open)[] entries = MenuCycle.Entries(builtGroup);
			if (entries == null || i < 0 || i >= entries.Length)
			{
				return;
			}
			MenuSystem.Close();
			if (EClass.ui != null && EClass.ui.TopLayer is LayerChara lc)
			{
				lc.Close();
			}
			try
			{
				entries[i].open();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys family bar jump failed: " + ex);
			}
		}
	}
}
