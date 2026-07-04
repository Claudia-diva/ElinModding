using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Elona-style in-world targeting: no window. Light screen dim, the candidate's
	// tile is highlighted, its name shown at the bottom; arrows cycle targets by
	// distance, Enter confirms, Escape/right-click cancels. The confirmed target
	// is REMEMBERED so later casts/throws fire instantly while it stays valid.
	public static class TargetMode
	{
		private static Chara lastTarget;

		private static List<Chara> candidates;
		private static int index;
		private static Action<Chara> onPick;
		private static string verb;
		private static GameObject overlay;
		private static Text label;

		public static bool Active => onPick != null;

		// instantly usable remembered target (or current enemy) for this act
		public static Chara QuickTarget(Act act)
		{
			Chara c = lastTarget;
			if (!Valid(c, act))
			{
				c = EClass.pc.enemy;
			}
			return Valid(c, act) ? c : null;
		}

		public static void Remember(Chara c)
		{
			lastTarget = c;
		}

		private static bool Valid(Chara c, Act act)
		{
			if (c == null || c.isDead || !c.IsAliveInCurrentZone || !EClass.pc.CanSee(c))
			{
				return false;
			}
			if (act == null)
			{
				return true;
			}
			try
			{
				return act.IsValidTC(c) && act.CanPerform(EClass.pc, c, c.pos);
			}
			catch
			{
				return false;
			}
		}

		public static void Begin(string _verb, List<Chara> _candidates, Action<Chara> _onPick)
		{
			Cancel();
			if (_candidates == null || _candidates.Count == 0)
			{
				Msg.SayRaw("No valid target in range.");
				return;
			}
			candidates = _candidates;
			onPick = _onPick;
			verb = _verb;
			index = 0;
			// start on the remembered target when it's among the candidates
			if (lastTarget != null)
			{
				int i = candidates.IndexOf(lastTarget);
				if (i >= 0)
				{
					index = i;
				}
			}
			BuildOverlay();
			RefreshLabel();
		}

		public static void Cancel()
		{
			onPick = null;
			candidates = null;
			if (overlay != null)
			{
				UnityEngine.Object.Destroy(overlay);
				overlay = null;
				label = null;
			}
		}

		private static void BuildOverlay()
		{
			overlay = new GameObject("ElonaKeysTarget");
			overlay.transform.SetParent(EClass.ui.transform, worldPositionStays: false);
			RectTransform rt = overlay.AddComponent<RectTransform>();
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			Image dim = overlay.AddComponent<Image>();
			dim.color = new Color(0f, 0f, 0f, 0.22f); // slight dim, no flashbang
			dim.raycastTarget = false;                 // mouse stays free
			GameObject lg = new GameObject("label");
			lg.transform.SetParent(overlay.transform, worldPositionStays: false);
			label = lg.AddComponent<Text>();
			Text existing = UnityEngine.Object.FindObjectOfType<Text>();
			label.font = (existing != null && existing.font != null) ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			label.fontSize = 20;
			label.alignment = TextAnchor.MiddleCenter;
			label.horizontalOverflow = HorizontalWrapMode.Overflow;
			label.color = new Color(1f, 0.95f, 0.7f);
			label.raycastTarget = false;
			RectTransform lrt = label.rectTransform;
			lrt.anchorMin = new Vector2(0.5f, 0f);
			lrt.anchorMax = new Vector2(0.5f, 0f);
			lrt.anchoredPosition = new Vector2(0f, 120f);
			lrt.sizeDelta = new Vector2(700f, 30f);
			Outline o = lg.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.95f);
			o.effectDistance = new Vector2(1.5f, -1.5f);
		}

		private static void RefreshLabel()
		{
			if ((bool)label && candidates != null && candidates.Count > 0)
			{
				Chara c = candidates[index];
				string tag = EClass.pc.IsHostile(c) ? " (hostile)" : (c.IsPCParty ? " (party)" : "");
				label.text = "► " + verb + ": " + c.Name + tag + " (" + EClass.pc.pos.Distance(c.pos) + ")   " + (index + 1) + "/" + candidates.Count + "   Enter = confirm, Esc = cancel";
			}
		}

		// called every frame from InputRouter while Active (input is fenced there)
		public static void HandleKeys()
		{
			if (!Active)
			{
				return;
			}
			// prune dead/vanished candidates live
			candidates.RemoveAll((Chara c) => c == null || c.isDead || !c.IsAliveInCurrentZone);
			if (candidates.Count == 0)
			{
				Cancel();
				return;
			}
			index = Mathf.Clamp(index, 0, candidates.Count - 1);
			// highlight the selected target's tile every frame
			try
			{
				candidates[index].pos.SetHighlight(8);
			}
			catch
			{
			}
			if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
			{
				Cancel();
				return;
			}
			if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
			{
				index = (index + 1) % candidates.Count;
				RefreshLabel();
				return;
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
			{
				index = (index - 1 + candidates.Count) % candidates.Count;
				RefreshLabel();
				return;
			}
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
			{
				Chara c = candidates[index];
				Action<Chara> cb = onPick;
				Cancel();
				lastTarget = c;
				try
				{
					cb(c);
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys target action failed: " + ex);
				}
			}
		}
	}
}
