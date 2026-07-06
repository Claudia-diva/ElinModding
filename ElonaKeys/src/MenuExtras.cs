using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElonaKeys
{
	// Per-frame extras driven from Plugin.Update():
	// - press X over a row in our menus to examine the item (LayerInfo)
	// - hover an item row (our menus) or a spell (LayerAbility) and press 0-9
	//   (shift for slots 11-20) to assign it to the Extra hotbar
	// - ping-pong scrolling for long names
	// Hold-to-repeat for NAVIGATION keys: fires on the initial press, then
	// repeats while held (Elona-style list scrolling). Confirm/cancel keys
	// stay single-shot.
	internal static class KeyRepeat
	{
		private const float Delay = 0.35f;
		private const float Interval = 0.07f;

		private static readonly Dictionary<KeyCode, float> next = new Dictionary<KeyCode, float>();

		public static bool Down(KeyCode k)
		{
			if (Input.GetKeyDown(k))
			{
				next[k] = Time.unscaledTime + Delay;
				return true;
			}
			if (Input.GetKey(k))
			{
				float t;
				if (!next.TryGetValue(k, out t))
				{
					next[k] = Time.unscaledTime + Delay;
					return false;
				}
				if (Time.unscaledTime >= t)
				{
					next[k] = Time.unscaledTime + Interval;
					return true;
				}
			}
			return false;
		}
	}

	public static class MenuExtras
	{
		private class Scroller
		{
			public UIText text;
			public string full;   // plain text (tags stripped): we scroll CONTENT
			public int budget;    // visible character window
			public float t;
		}

		private static readonly List<Scroller> scrollers = new List<Scroller>();
		private static LayerList currentMenu;

		private static readonly KeyCode[] digitKeys =
		{
			KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
			KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
		};

		public static void RegisterMenu(LayerList layer)
		{
			currentMenu = layer;
		}

		public static bool MenuOpen => MenuSystem.Alive || (currentMenu != null && currentMenu);

		// digit index 0-9 where key "1" = 0 ... "0" = 9; -1 if none pressed this frame
		public static int GetDigitDown()
		{
			for (int i = 0; i < digitKeys.Length; i++)
			{
				if (Input.GetKeyDown(digitKeys[i]))
				{
					return i;
				}
			}
			return -1;
		}

		public static void OnUpdate()
		{
			try
			{
				UpdateScrollers();
				if (EClass.core == null || !EClass.core.IsGameStarted)
				{
					return;
				}
				MenuSystem.HandleKeys();
				PopupNav.OnUpdate();
				ShiftOverlay.OnUpdate();
				UpdateAssignKey();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys MenuExtras failed: " + ex);
			}
		}

		// mouse-hovered menu row, if any (x-examine and hotkey assignment)
		public static Thing HoveredThing()
		{
			ItemGeneral item = InputModuleEX.GetComponentOf<ItemGeneral>();
			if ((bool)item && item.card is Thing t)
			{
				return t;
			}
			return null;
		}

		// ---------- 0-9 hotbar assignment ----------

		private static void UpdateAssignKey()
		{
			int digit = GetDigitDown();
			// plain digit, ctrl+digit (Elona style) and shift+digit all BIND
			// inside menus; only alt stays out (native alt combos)
			if (digit == -1 || digit >= 9 || EInput.isAltDown)
			{
				return; // quickbind slots are 1-9
			}
			SourceElement.Row src = null;
			Thing item = null;
			if (MenuOpen)
			{
				item = HoveredThing() ?? MenuSystem.SelectedThing();
				if (item == null)
				{
					src = MenuSystem.SelectedActSrc();
				}
			}
			if (src == null && item == null && (bool)LayerAbility.Instance)
			{
				ButtonAbility ba = InputModuleEX.GetComponentOf<ButtonAbility>();
				if ((bool)ba && ba.source != null)
				{
					src = ba.source;
				}
			}
			if (src == null && item == null)
			{
				return;
			}
			// digits in menus bind ElonaKeys' OWN registry (Quickbind): the
			// game's quickslot bar is never touched anymore
			if (src != null)
			{
				Quickbind.AssignAbility(digit, src);
			}
			else
			{
				Quickbind.AssignItem(digit, item);
			}
		}

		// ---------- long name display ----------

		public static void ApplyNameDisplay(UIText text, float maxWidth)
		{
			// rows are pooled and rebuilt on every page/selection change: never let
			// two scrollers fight over the same text component
			for (int i = scrollers.Count - 1; i >= 0; i--)
			{
				if (scrollers[i].text == text)
				{
					scrollers.RemoveAt(i);
				}
			}
			string mode = Plugin.NameDisplay;
			if (mode == "wrap")
			{
				text.horizontalOverflow = HorizontalWrapMode.Wrap;
				return;
			}
			text.horizontalOverflow = HorizontalWrapMode.Overflow;
			float pref = text.preferredWidth;
			// slack: tiny overflows render as-is instead of scrolling forever
			if (pref <= maxWidth + Plugin.NameScrollSlack)
			{
				return;
			}
			// scroll the CONTENT, Elona style: the text component never moves, so
			// nothing slides over the sprite/letter badge on the left
			string plain = text.text;
			if (plain.IndexOf('<') >= 0)
			{
				plain = System.Text.RegularExpressions.Regex.Replace(plain, "<.*?>", "");
			}
			int budget = Math.Max(4, (int)(plain.Length * maxWidth / pref) - 1);
			if (mode == "pingpong" && budget < plain.Length)
			{
				text.text = plain.Substring(0, budget);
				scrollers.Add(new Scroller
				{
					text = text,
					full = plain,
					budget = budget,
					t = 0f
				});
			}
			else // truncate (or nothing to scroll)
			{
				if (budget < plain.Length)
				{
					text.text = plain.Substring(0, Math.Max(4, budget - 1)) + "…";
				}
			}
		}

		private static void UpdateScrollers()
		{
			for (int i = scrollers.Count - 1; i >= 0; i--)
			{
				Scroller s = scrollers[i];
				if (s.text == null || !s.text)
				{
					scrollers.RemoveAt(i);
					continue;
				}
				int range = s.full.Length - s.budget;
				if (range <= 0)
				{
					scrollers.RemoveAt(i);
					continue;
				}
				s.t += Time.unscaledDeltaTime * 0.5f;
				float k = Mathf.PingPong(s.t, 1.2f);
				float frac = Mathf.Clamp01((k - 0.1f) / 1f); // brief pause at each end
				int off = Mathf.Clamp(Mathf.RoundToInt(frac * range), 0, range);
				s.text.text = s.full.Substring(off, s.budget);
			}
		}
	}
}
