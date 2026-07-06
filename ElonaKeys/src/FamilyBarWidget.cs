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
		private UIImage bg;
		private float builtScale = -1f;
		private float lastErr;

		public override void OnActivate()
		{
			font = RadialWheel.FindFont();
			// fresh config sits at screen center: park the bar top-center
			if (config.x == 0f && config.y == 0f && config.userAnchor == RectPosition.Auto)
			{
				SetAnchor(RectPosition.TopCenter);
				(base.transform as RectTransform).anchoredPosition = new Vector2(0f, -70f);
			}
		}

		private void Update()
		{
			try
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
				if (content != null && g == builtGroup && idx == builtIndex && builtScale == Plugin.FamilyBarScale)
				{
					content.SetActive(value: true);
					return;
				}
				Rebuild(g, idx);
			}
			catch (Exception ex)
			{
				if (Time.unscaledTime - lastErr > 5f)
				{
					lastErr = Time.unscaledTime;
					Plugin.Log?.LogError("ElonaKeys family bar update failed: " + ex);
				}
			}
		}

		// widget right-click: native base options + our size/hint controls
		public override void OnSetContextMenu(UIContextMenu m)
		{
			m.AddSlider("Bar size", (float n) => n.ToString("0.00"), Plugin.FamilyBarScale, delegate(float a)
			{
				Plugin.FamilyBarScale = a;
				builtScale = -1f; // rebuild next frame
			}, 0.4f, 1.5f);
			m.AddToggle("Show Tab hint", Plugin.FamilyBarHint, delegate(bool a)
			{
				Plugin.FamilyBarHint = a;
				builtScale = -1f;
			});
			SetBaseContextMenu(m);
		}

		private void Rebuild(string g, int idx)
		{
			builtGroup = g;
			builtIndex = idx;
			builtScale = Plugin.FamilyBarScale;
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
			content.transform.localScale = new Vector3(builtScale, builtScale, 1f);
			float width = entries.Length * Spacing + 40f;
			(base.transform as RectTransform).sizeDelta = new Vector2(width * builtScale, 120f * builtScale);
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
			// hint line, like Elona's ",Tab [Change]" (optional)
			if (Plugin.FamilyBarHint)
			{
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
		}

		private static void AddOutline(GameObject go)
		{
			Outline o = go.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.9f);
			o.effectDistance = new Vector2(1.2f, -1.2f);
		}

		// the game's own sliced skin panel: a UIImage of type BG_Window bound to
		// this widget's SkinRoot; the native style menu (skin/size/color/decos)
		// then works exactly like on built-in widgets
		private void BuildBg(RectTransform parent, float width)
		{
			GameObject go = new GameObject("bg");
			go.transform.SetParent(parent, worldPositionStays: false);
			bg = go.AddComponent<UIImage>();
			bg.imageType = ImageType.BG_Window;
			bg.skinRoot = base.skinRoot;
			bg.raycastTarget = false;
			RectTransform r = bg.rectTransform;
			r.anchorMin = new Vector2(0.5f, 0.5f);
			r.anchorMax = new Vector2(0.5f, 0.5f);
			go.transform.SetAsFirstSibling();
			try
			{
				// SkinRoot sizes BG_Window as skin-default + sizeFix (+ cfg.bgSize)
				SkinAsset_BG b = config.skin.BG;
				bg.sizeFix = new Vector2(width, 100f) - new Vector2(b.size.x, b.size.y);
			}
			catch
			{
				bg.sizeFix = new Vector2(width - 320f, -140f);
			}
			try
			{
				ApplySkin();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys family bar skin failed: " + ex);
			}
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
