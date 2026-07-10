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
		// TWO locks (user spec): a HOSTILE lock for offense and an ALLY lock
		// for heals/buffs, held at the same time. Offensive casts NEVER
		// auto-fire at the ally lock; holding SHIFT while casting harms the
		// locked ally deliberately. With no hostile lock, offense
		// auto-acquires the nearest visible enemy — combat stays pure spam.
		private static Chara lastHostile;
		private static Chara lastFriendly;

		private static Point cursor;
		private static List<Chara> candidates;
		private static Action<Point, Chara> onPick;
		private static string verb;
		private static GameObject overlay;
		private static Text label;

		public static bool Active => onPick != null;

		// where the cursor should start: the hostile lock leads
		private static Chara lastTarget => AliveOr(lastHostile) ?? AliveOr(lastFriendly);

		private static Chara AliveOr(Chara c)
		{
			return (c != null && !c.isDead && c.IsAliveInCurrentZone && EClass.pc.CanSee(c)) ? c : null;
		}

		public static Point SelectedPos()
		{
			return Active ? cursor : null;
		}

		// numpad/ : the next cast aims at the OPPOSITE lock. Tap to arm
		// (one cast), tap again to cancel; holding it while casting works
		// too. This is the ONLY way to cross the streams — hostile at your
		// ally, heals/buffs at the enemy. Shift is NOT a modifier here:
		// shift+1-9 fires quickbinds and must never re-aim them.
		private static bool swapArmed;

		public static void ToggleSwap()
		{
			swapArmed = !swapArmed;
			Msg.SayRaw(swapArmed
				? "Swap armed: the NEXT cast aims at the opposite lock (hostile at your ally, buffs/heals at the enemy)."
				: "Swap canceled.");
			RefreshLockHud();
		}

		// ---- the always-on lock readout (top center): nobody reads configs,
		// the screen has to SAY what the locks are and that swap is armed ----
		private static GameObject lockHud;
		private static Text lockHudText;
		private static string lockHudLast;

		public static void HideLockHud()
		{
			lockHudLast = null;
			if ((bool)lockHud)
			{
				lockHud.SetActive(false);
			}
		}

		// called every frame from InputRouter (cheap: bails on same string)
		public static void RefreshLockHud()
		{
			Chara foe = AliveOr(lastHostile);
			Chara pal = AliveOr(lastFriendly);
			string s = "";
			if (foe != null)
			{
				s += "<color=#ff9090>Enemy: " + foe.Name + "</color>   ";
			}
			if (pal != null)
			{
				s += "<color=#90d090>Ally: " + (pal == EClass.pc ? "yourself" : pal.Name) + "</color>   ";
			}
			if (swapArmed)
			{
				s += "<color=#ffd070>SWAP armed: next cast at the OPPOSITE lock</color>";
			}
			s = s.TrimEnd();
			if (s == lockHudLast)
			{
				return;
			}
			lockHudLast = s;
			if (string.IsNullOrEmpty(s) || Plugin.LockHudSize <= 0)
			{
				if ((bool)lockHud)
				{
					lockHud.SetActive(false);
				}
				return;
			}
			if (!lockHud || !lockHudText)
			{
				BuildLockHud();
			}
			// position/size re-applied on every text change: config edits
			// (modconfig) land without a restart
			lockHudText.fontSize = Plugin.LockHudSize;
			lockHudText.rectTransform.anchoredPosition = new Vector2(Plugin.LockHudX, -Plugin.LockHudY);
			lockHud.SetActive(true);
			lockHudText.text = s;
		}

		private static void BuildLockHud()
		{
			if ((bool)lockHud)
			{
				UnityEngine.Object.Destroy(lockHud);
			}
			lockHud = new GameObject("ElonaKeysLocks");
			lockHud.transform.SetParent(EClass.ui.transform, worldPositionStays: false);
			lockHudText = lockHud.AddComponent<Text>();
			lockHudText.font = UiFont();
			lockHudText.fontSize = Plugin.LockHudSize;
			lockHudText.alignment = TextAnchor.UpperCenter;
			lockHudText.horizontalOverflow = HorizontalWrapMode.Overflow;
			lockHudText.verticalOverflow = VerticalWrapMode.Overflow;
			lockHudText.color = new Color(1f, 0.97f, 0.85f);
			lockHudText.raycastTarget = false;
			RectTransform rt = lockHudText.rectTransform;
			rt.anchorMin = new Vector2(0.5f, 1f);
			rt.anchorMax = new Vector2(0.5f, 1f);
			rt.pivot = new Vector2(0.5f, 1f);
			rt.anchoredPosition = new Vector2(Plugin.LockHudX, -Plugin.LockHudY);
			rt.sizeDelta = new Vector2(900f, 28f);
			Outline o = lockHud.AddComponent<Outline>();
			o.effectColor = new Color(0f, 0f, 0f, 0.95f);
			o.effectDistance = new Vector2(1.2f, -1.2f);
		}

		private static bool ConsumeSwap()
		{
			if (swapArmed)
			{
				swapArmed = false;
				return true;
			}
			return Plugin.BindHeld("SwapTarget");
		}

		// instantly usable target for this act; act == null = hostile intent
		// (throws, ranged). TWO CLASSES, TWO LOCKS: hostile acts aim at the
		// enemy lock (or auto-acquire the nearest foe) and NEVER at your
		// people; everything else aims at the ally lock — which may be
		// YOURSELF — and never at a foe. numpad/ swaps for one cast.
		public static Chara QuickTarget(Act act)
		{
			bool hostile = HostileAct(act);
			if (ConsumeSwap())
			{
				hostile = !hostile;
			}
			if (hostile)
			{
				Chara c = AliveOr(lastHostile);
				if (c != null && IsFoe(c))
				{
					return c;
				}
				c = AliveOr(EClass.player.target);
				if (c != null && IsFoe(c))
				{
					return c;
				}
				c = AliveOr(EClass.pc.enemy);
				if (c != null && IsFoe(c))
				{
					return c;
				}
				return NearestFoe();
			}
			// beneficial (or swapped-hostile): the ally lock; null = the
			// act's normal flow takes over (self-cast, member picker...)
			return AliveOr(lastFriendly);
		}

		private static bool IsFoe(Chara c)
		{
			try
			{
				return EClass.pc.IsHostile(c) && !c.IsPCParty;
			}
			catch
			{
				return false;
			}
		}

		private static Chara NearestFoe()
		{
			Chara best = null;
			int bestDist = int.MaxValue;
			try
			{
				foreach (Chara c in EClass._map.charas)
				{
					if (c == EClass.pc || c.isDead || !EClass.pc.CanSee(c) || !IsFoe(c))
					{
						continue;
					}
					int d = EClass.pc.pos.Distance(c.pos);
					if (d < bestDist)
					{
						bestDist = d;
						best = c;
					}
				}
			}
			catch
			{
			}
			return best;
		}

		// hostile = the game's OWN signals, all four: an enemy-only target
		// type, an act that declares IsHostileAct (bloodsuck, steal...),
		// the "neg" element tag (what ACT.CanPerform uses to protect your
		// party, ACT.cs:355; what colors abilities red, ButtonElement.cs:108),
		// or an offensive abilityType on the element row (attack / dot /
		// debuff — the same column our menu buckets read). Melee arts like
		// bladestorm carry ONLY the abilityType; they have no tag and no
		// enemy-only target type, which is how they kept reading friendly.
		internal static bool HostileAct(Act act)
		{
			if (act == null)
			{
				return true; // throws, ranged fire
			}
			try
			{
				if (act.IsHostileAct || act.TargetType.CanOnlyTargetEnemy || act.HasTag("neg"))
				{
					return true;
				}
				string[] at = act.source.abilityType;
				if (at != null && at.Length > 0)
				{
					string t = at[0];
					if (t == "attack" || t == "dot" || t == "debuff" || t == "debuffStats")
					{
						return true;
					}
				}
			}
			catch
			{
				return true;
			}
			return false;
		}

		public static void Remember(Chara c)
		{
			// YOURSELF is a valid ally lock (solo play: keep your buffs on
			// you while attacks auto-acquire); attacks never read this side
			if (c == null)
			{
				return;
			}
			if (IsFoe(c))
			{
				lastHostile = c;
				// the NATIVE target: the on-map marker and ranged auto-fire.
				// Only for FOES — pointing it at an ally would auto-shoot her
				try
				{
					if (EClass.player.target != c)
					{
						EClass.player.target = c;
					}
				}
				catch
				{
				}
			}
			else
			{
				lastFriendly = c;
			}
		}

		// the frame Begin ran: the confirm key that OPENED this aim (Enter on
		// a menu row) must not also confirm the aim in the same frame
		private static int beginFrame = -1;

		public static void Begin(string _verb, List<Chara> _candidates, Action<Point, Chara> _onPick, bool preferFoe = false)
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
			beginFrame = Time.frameCount;
			// cursor start: hostile aims begin on a FOE (the enemy lock or the
			// nearest hostile candidate), never on the ally lock or yourself;
			// everything else starts on the remembered target
			Chara start = null;
			if (preferFoe)
			{
				start = AliveOr(lastHostile);
				if (start != null && !IsFoe(start))
				{
					start = null;
				}
			}
			else if (lastTarget != null && !lastTarget.isDead && lastTarget.IsAliveInCurrentZone && EClass.pc.CanSee(lastTarget))
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

		// the game's REAL UI font (skin-configured): an arbitrary
		// FindObjectOfType<Text> font renders NOTHING half the time — the
		// exact failure the column headers went through (v1 lesson)
		private static Font UiFont()
		{
			try
			{
				Font f = SkinManager.Instance?.fontSet?.ui?.source?.font;
				if (f != null)
				{
					return f;
				}
			}
			catch
			{
			}
			try
			{
				return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			}
			catch
			{
				return null;
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
			if (Plugin.AimLabelSize <= 0)
			{
				return; // label hidden by config; the cursor still works
			}
			GameObject lg = new GameObject("label");
			lg.transform.SetParent(overlay.transform, worldPositionStays: false);
			label = lg.AddComponent<Text>();
			label.font = UiFont();
			label.fontSize = Plugin.AimLabelSize;
			label.verticalOverflow = VerticalWrapMode.Overflow;
			label.alignment = TextAnchor.MiddleCenter;
			label.horizontalOverflow = HorizontalWrapMode.Overflow;
			label.color = new Color(1f, 0.95f, 0.7f);
			label.raycastTarget = false;
			RectTransform lrt = label.rectTransform;
			lrt.anchorMin = new Vector2(0.5f, 0f);
			lrt.anchorMax = new Vector2(0.5f, 0f);
			lrt.anchoredPosition = new Vector2(0f, Plugin.AimLabelY);
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
			// enemy highlight, same as the game's own (AM_Adv.OnRenderTile):
			// respects the "highlight enemies" game setting — the native pass
			// doesn't always run while we own the input
			try
			{
				if (EClass.core.config.game.highlightEnemy)
				{
					foreach (Chara ch in EClass._map.charas)
					{
						if (ch.isSynced && ch.IsHostile(EClass.pc) && !ch.IsMultisize && EClass.pc.CanSee(ch) && (ch.mimicry == null || !ch.mimicry.IsThing))
						{
							Vector3 hv = ch.pos.Position();
							EClass.screen.guide.passGuideFloor.Add(hv.x, hv.y, hv.z - 0.01f, 23f, 0.3f);
						}
					}
				}
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
			if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && Time.frameCount != beginFrame)
			{
				Point p = cursor.Copy();
				Chara c = CursorChara();
				Action<Point, Chara> cb = onPick;
				Cancel();
				if (c != null)
				{
					Remember(c); // routes into the right lock (enemy vs ally)
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
