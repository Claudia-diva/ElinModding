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
	public static class MenuExtras
	{
		private class Scroller
		{
			public UIText text;
			public float baseX;
			public float range;
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
				ShiftBarWidget.EnsureAndUpdate();
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
			if (digit == -1 || digit >= 9 || EInput.isCtrlDown || EInput.isAltDown)
			{
				return; // quickslots are 1-9 (digit 0 has no native slot)
			}
			// which of the two quickslot pages: plain = shown page, shift = other
			int page = EClass.player.hotbarPage;
			if (EInput.isShiftDown)
			{
				page = (page == 0) ? 1 : 0;
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
			AssignQuickslot(page, digit, src, item);
		}

		// The quickslot bar is pc.things with invY=1, invX = page*10 + slot
		// (WidgetCurrentTool). This mirrors the native drag-assign at
		// ActionMode.cs:595-609.
		private static void AssignQuickslot(int page, int slot, SourceElement.Row src, Thing item)
		{
			try
			{
				int invX = page * 10 + slot;
				// evict whatever occupies the slot: abilities are destroyed,
				// real items go back to the main inventory
				foreach (Thing t in EClass.pc.things.List((Thing x) => x.invY == 1 && x.invX == invX, onlyAccessible: false))
				{
					if (t.trait is TraitAbility)
					{
						t.Destroy();
					}
					else
					{
						t.invY = 0;
						t.invX = 0;
					}
				}
				string what;
				if (src != null)
				{
					Thing ability = ThingGen.Create("ability");
					ability.c_idAbility = src.alias;
					EClass.pc.AddThing(ability, tryStack: false, invX, 1);
					ability.invY = 1;
					ability.invX = invX;
					what = src.GetName();
				}
				else
				{
					item.invX = invX;
					item.invY = 1;
					what = item.Name;
				}
				WidgetCurrentTool.dirty = true;
				ShiftBarWidget.SetDirty();
				SE.Equip();
				Msg.SayRaw(what + " set to quickslot " + (slot + 1) + (EInput.isShiftDown ? " (other page)." : "."));
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys quickslot assign failed: " + ex);
				SE.BeepSmall();
			}
		}

		// shift+digit: fire slot N of the NON-displayed quickslot page without
		// flipping pages (replicates WidgetCurrentTool.Select's ability auto-use)
		public static void UseOtherPageSlot(int slot)
		{
			int page = (EClass.player.hotbarPage == 0) ? 1 : 0;
			int invX = page * 10 + slot;
			Thing found = null;
			foreach (Thing t in EClass.pc.things)
			{
				if (t.invY == 1 && t.invX == invX)
				{
					// abilities win when a stray duplicate occupies the slot
					if (found == null || t.trait is TraitAbility)
					{
						found = t;
					}
				}
			}
			if (found == null)
			{
				SE.BeepSmall();
				return;
			}
			if (found.trait is TraitAbility ability)
			{
				if (ability.CanUse(EClass.pc) && EClass.player.CanAcceptInput() && ability.OnUse(EClass.pc))
				{
					EClass.player.EndTurn();
				}
			}
			else if (found.trait.CanUse(EClass.pc))
			{
				found.trait.OnUse(EClass.pc);
			}
			else
			{
				EClass.pc.HoldCard(found);
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
					Vector2 pos = text.rectTransform.anchoredPosition;
					pos.x = scrollers[i].baseX;
					text.rectTransform.anchoredPosition = pos;
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
			if (pref <= maxWidth)
			{
				return;
			}
			if (mode == "pingpong")
			{
				scrollers.Add(new Scroller
				{
					text = text,
					baseX = text.rectTransform.anchoredPosition.x,
					range = pref - maxWidth,
					t = 0f
				});
			}
			else // truncate
			{
				string s = text.text;
				int keep = Math.Max(4, (int)(s.Length * maxWidth / pref) - 2);
				if (keep < s.Length)
				{
					text.text = s.Substring(0, keep) + "…";
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
				s.t += Time.unscaledDeltaTime * 0.5f;
				float k = Mathf.PingPong(s.t, 1.2f);
				float shift = Mathf.Clamp01(k - 0.1f) * s.range; // brief pause at each end
				Vector2 pos = s.text.rectTransform.anchoredPosition;
				pos.x = s.baseX - shift;
				s.text.rectTransform.anchoredPosition = pos;
			}
		}
	}
}
