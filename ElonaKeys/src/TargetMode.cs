using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Elona-style in-world targeting: no window. A free tile CURSOR moves with
	// the arrow keys (iso-aware) and numpad diagonals, anywhere within vision;
	// Space jumps between character candidates (hostiles first), Enter confirms
	// the tile, Escape/right-click cancels. While active, the cursor tile is fed
	// into the game's mouse-hover pipeline, so the hover info card follows it.
	// A confirmed character is REMEMBERED so later casts/throws fire instantly.
	public static class TargetMode
	{
		private static Chara lastTarget;

		private static Point cursor;
		private static List<Chara> candidates;
		private static Action<Point, Chara> onPick;
		private static string verb;
		private static GameObject overlay;
		private static Text label;

		public static bool Active => onPick != null;

		public static Point SelectedPos()
		{
			return Active ? cursor : null;
		}

		// instantly usable remembered target (or current enemy) for this act;
		// act == null means a hostile intent (throw)
		public static Chara QuickTarget(Act act)
		{
			bool needHostile = act == null || SafeHostileAct(act);
			Chara c = lastTarget;
			if (!Valid(c, act, needHostile))
			{
				c = EClass.player.target; // the native key's pick counts too
			}
			if (!Valid(c, act, needHostile))
			{
				c = EClass.pc.enemy;
			}
			return Valid(c, act, needHostile) ? c : null;
		}

		private static bool SafeHostileAct(Act act)
		{
			try
			{
				return act.IsHostileAct;
			}
			catch
			{
				return true;
			}
		}

		public static void Remember(Chara c)
		{
			lastTarget = c;
			// feed the NATIVE target too: that's what draws the on-map target
			// marker and what auto-fire reads — with only our private memory,
			// * left no visible indicator on the picked character
			try
			{
				if (c != null && EClass.player.target != c)
				{
					EClass.player.target = c;
				}
			}
			catch
			{
			}
		}

		private static bool Valid(Chara c, Act act, bool needHostile)
		{
			if (c == null || c.isDead || !c.IsAliveInCurrentZone || !EClass.pc.CanSee(c))
			{
				return false;
			}
			// throws (act == null) keep the hostile preference so they never
			// auto-hit a friend; SPELLS force-cast at the remembered target,
			// friend or foe (user directive) — the game validates on cast
			if (act == null && needHostile && (!EClass.pc.IsHostile(c) || c.IsPCParty))
			{
				return false;
			}
			return true;
		}

		public static void Begin(string _verb, List<Chara> _candidates, Action<Point, Chara> _onPick)
		{
			Cancel();
			candidates = _candidates ?? new List<Chara>();
			// hostiles first, then by distance: Space cycles in this order
			candidates.Sort(delegate(Chara a, Chara b)
			{
				bool ha = EClass.pc.IsHostile(a);
				bool hb = EClass.pc.IsHostile(b);
				if (ha != hb)
				{
					return ha ? -1 : 1;
				}
				return EClass.pc.pos.Distance(a.pos) - EClass.pc.pos.Distance(b.pos);
			});
			onPick = _onPick;
			verb = _verb;
			// cursor: remembered target, else nearest hostile, else nearest, else self
			Chara start = null;
			if (lastTarget != null && !lastTarget.isDead && lastTarget.IsAliveInCurrentZone && EClass.pc.CanSee(lastTarget))
			{
				start = lastTarget;
			}
			if (start == null && candidates.Count > 0)
			{
				start = candidates[0];
			}
			cursor = ((start != null) ? start.pos : EClass.pc.pos).Copy();
			BuildOverlay();
			RefreshLabel();
		}

		public static void Cancel()
		{
			onPick = null;
			candidates = null;
			cursor = null;
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
			lrt.anchoredPosition = new Vector2(0f, 130f);
			lrt.sizeDelta = new Vector2(760f, 58f);
			Outline o = lg.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.95f);
			o.effectDistance = new Vector2(1.5f, -1.5f);
		}

		private static Chara CursorChara()
		{
			try
			{
				return cursor?.FirstVisibleChara();
			}
			catch
			{
				return null;
			}
		}

		private static void RefreshLabel()
		{
			if (!(bool)label || cursor == null)
			{
				return;
			}
			Chara c = CursorChara();
			string line1;
			string line2;
			if (c != null)
			{
				string tag = EClass.pc.IsHostile(c) ? " <color=#ff9090>(hostile)</color>" : (c.IsPCParty ? " <color=#90d090>(party)</color>" : "");
				line1 = "► " + verb + ": " + c.Name + tag;
				string details = "";
				try
				{
					details = "Lv" + c.LV + "   HP " + c.hp + "/" + c.MaxHP + "   dist " + EClass.pc.pos.Distance(c.pos);
				}
				catch
				{
				}
				line2 = details;
			}
			else
			{
				line1 = "► " + verb + ": (tile)";
				line2 = "dist " + EClass.pc.pos.Distance(cursor);
			}
			label.text = line1 + "\n<size=16>" + line2 + "   |   arrows move, Space next target, Enter confirm, Esc cancel</size>";
		}

		// screen-direction key → tile delta (iso-aware like the direction prompts)
		private static Vector2 ReadDir()
		{
			Vector2 v = Vector2.zero;
			if (KeyRepeat.Down(KeyCode.LeftArrow) || KeyRepeat.Down(KeyCode.Keypad4))
			{
				v = new Vector2(-1f, 0f);
			}
			else if (KeyRepeat.Down(KeyCode.RightArrow) || KeyRepeat.Down(KeyCode.Keypad6))
			{
				v = new Vector2(1f, 0f);
			}
			else if (KeyRepeat.Down(KeyCode.UpArrow) || KeyRepeat.Down(KeyCode.Keypad8))
			{
				v = new Vector2(0f, 1f);
			}
			else if (KeyRepeat.Down(KeyCode.DownArrow) || KeyRepeat.Down(KeyCode.Keypad2))
			{
				v = new Vector2(0f, -1f);
			}
			else if (KeyRepeat.Down(KeyCode.Keypad7))
			{
				v = new Vector2(-1f, 1f);
			}
			else if (KeyRepeat.Down(KeyCode.Keypad9))
			{
				v = new Vector2(1f, 1f);
			}
			else if (KeyRepeat.Down(KeyCode.Keypad1))
			{
				v = new Vector2(-1f, -1f);
			}
			else if (KeyRepeat.Down(KeyCode.Keypad3))
			{
				v = new Vector2(1f, -1f);
			}
			if (v != Vector2.zero && Plugin.IsoAim)
			{
				v = DirPrompt.IsoRotate(v);
			}
			return v;
		}

		// called every frame from InputRouter while Active (input is fenced there)
		public static void HandleKeys()
		{
			if (!Active)
			{
				return;
			}
			candidates.RemoveAll((Chara c) => c == null || c.isDead || !c.IsAliveInCurrentZone);
			// highlight the cursor tile every frame
			try
			{
				cursor.SetHighlight(8);
			}
			catch
			{
			}
			if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
			{
				Cancel();
				return;
			}
			Vector2 d = ReadDir();
			if (d != Vector2.zero)
			{
				Point np = cursor.Copy();
				np.x += (int)d.x;
				np.z += (int)d.y;
				bool ok = np.IsValid;
				if (ok)
				{
					try
					{
						ok = EClass.pc.CanSeeSimple(np);
					}
					catch
					{
						ok = false;
					}
				}
				if (ok)
				{
					cursor = np;
					RefreshLabel();
				}
				else
				{
					SE.BeepSmall(); // can't target outside your vision
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.Space) && candidates.Count > 0)
			{
				// jump to the next candidate after the one under the cursor
				int cur = -1;
				Chara here = CursorChara();
				if (here != null)
				{
					cur = candidates.IndexOf(here);
				}
				Chara next = candidates[(cur + 1) % candidates.Count];
				cursor = next.pos.Copy();
				RefreshLabel();
				return;
			}
			if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
			{
				Point p = cursor.Copy();
				Chara c = CursorChara();
				Action<Point, Chara> cb = onPick;
				Cancel();
				if (c != null)
				{
					lastTarget = c;
				}
				try
				{
					cb(p, c);
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys target action failed: " + ex);
				}
			}
		}
	}
}
