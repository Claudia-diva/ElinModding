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

		public static bool MenuOpen => currentMenu != null && currentMenu;

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
				UpdateExamineKey();
				UpdateAssignKey();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys MenuExtras failed: " + ex);
			}
		}

		// ---------- x to examine ----------

		private static void UpdateExamineKey()
		{
			if (!MenuOpen || !Input.GetKeyDown(KeyCode.X) || EInput.isCtrlDown || EInput.isAltDown)
			{
				return;
			}
			Thing t = HoveredThing();
			if (t != null)
			{
				EClass.ui.AddLayerDontCloseOthers<LayerInfo>().SetThing(t);
			}
		}

		private static Thing HoveredThing()
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
			if (digit == -1 || EInput.isCtrlDown || EInput.isAltDown)
			{
				return;
			}
			int slot = digit + (EInput.isShiftDown ? 10 : 0);
			HotItem hotItem = null;
			string what = null;
			if (MenuOpen)
			{
				Thing t = HoveredThing();
				if (t != null)
				{
					hotItem = new HotItemThing { thing = t };
					what = t.Name;
				}
			}
			if (hotItem == null && (bool)LayerAbility.Instance)
			{
				ButtonAbility ba = InputModuleEX.GetComponentOf<ButtonAbility>();
				if ((bool)ba && ba.source != null)
				{
					hotItem = new HotItemAct(ba.source);
					what = ba.source.GetName();
				}
			}
			if (hotItem == null)
			{
				return;
			}
			AssignToExtraHotbar(hotItem, slot, what);
		}

		private static void AssignToExtraHotbar(HotItem item, int slot, string what)
		{
			WidgetHotbar widget = WidgetHotbar.HotbarExtra;
			if (!widget || widget.hotbar == null)
			{
				Msg.SayRaw("The extra hotbar is not available.");
				return;
			}
			try
			{
				Hotbar bar = widget.hotbar;
				if (bar.itemsPerPage <= slot)
				{
					bar.SetSlotNum(slot + 1);
				}
				bar.SetItem(item, slot, -1, refreshActor: true);
				SE.Equip();
				Msg.SayRaw(what + " assigned to hotkey " + (slot + 1) + ".");
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys hotbar assign failed: " + ex);
				SE.BeepSmall();
			}
		}

		public static void UseHotbarSlot(int slot)
		{
			WidgetHotbar widget = WidgetHotbar.HotbarExtra;
			if (!widget || widget.hotbar == null)
			{
				SE.BeepSmall();
				return;
			}
			HotItem item = widget.hotbar.CurrentPage.GetItem(slot);
			if (item == null)
			{
				SE.BeepSmall();
				Msg.SayRaw("Hotkey " + (slot + 1) + " is empty. Hover an item or spell in a menu and press the digit to assign it.");
			}
			else
			{
				widget.TryUse(slot);
			}
		}

		// ---------- long name display ----------

		public static void ApplyNameDisplay(UIText text, float maxWidth)
		{
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
