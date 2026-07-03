using System;
using HarmonyLib;
using UnityEngine;

namespace ElonaKeys
{
	// Input interception happens in two stages:
	//
	// 1. EInput.Update postfix (right after the game maps keys to EInput.action,
	//    BEFORE ActionMode.UpdateInput consumes it): detect our binds and, with
	//    OverrideGameKeys, clear EInput.action so native menu toggles
	//    (chara/journal/ability/inventory at ActionMode.UpdateInput:749) never fire
	//    for keys the user gave to this mod. Digit hotkey activation is detected
	//    here too (suppressing the native toolbelt EInput.hotkey when configured).
	//
	// 2. AM_Adv._OnUpdateInput prefix (the game's own "gameplay keys" spot, gated
	//    by game state): execute the pending handler. If the frame passes without
	//    reaching AM_Adv (other action mode, modal UI), the pending bind is dropped.
	public static class InputRouter
	{
		private static Plugin.Entry pending;
		private static int pendingFrame = -1;

		private static float lastDebugLog;

		internal static void OnEInputUpdated()
		{
			try
			{
				Stage1();
			}
			catch (Exception ex)
			{
				// never let an exception take the whole pipeline down
				if (Time.unscaledTime - lastDebugLog > 5f)
				{
					lastDebugLog = Time.unscaledTime;
					Plugin.Log?.LogError("ElonaKeys stage1 failed: " + ex);
				}
			}
		}

		private static void Stage1()
		{
			if (pending != null && Time.frameCount > pendingFrame)
			{
				pending = null; // stale: last frame never reached adventure-mode input
				Debug("pending bind dropped (input not processed in adventure mode)");
			}
			if (DirPrompt.Active)
			{
				return;
			}
			if (EClass.core == null || !EClass.core.IsGameStarted || EClass.pc == null || EClass.ui == null)
			{
				return;
			}
			if (EInput.isInputFieldActive || EInput.isCtrlDown || EInput.isAltDown || EInput.IsConsumed)
			{
				return;
			}
			if (EClass.ui.BlockInput)
			{
				DebugGate("ui.BlockInput");
				return;
			}
			EAction current = EInput.action;
			bool over = Plugin.OverrideGameKeys;
			// without override the game's own keys win (ShowGrid = hardcoded 'g', always overridable)
			if (!over && current != EAction.None && current != EAction.ShowGrid)
			{
				return;
			}
			Plugin.Entry entry = Plugin.FindPressed();
			if (entry != null)
			{
				if (entry.action != EAction.None)
				{
					EInput.action = entry.action; // native passthrough (Fire)
					return;
				}
				EInput.action = EAction.None; // suppress whatever the game mapped to this key
				pending = entry;
				pendingFrame = Time.frameCount;
				return;
			}
			CheckDigitActivation();
		}

		// Digit keys fire Extra-hotbar slots assigned via the menus.
		private static void CheckDigitActivation()
		{
			string mode = Plugin.ActivateHotkeys;
			if (mode != "shift" && mode != "plain")
			{
				return;
			}
			int digit = MenuExtras.GetDigitDown(); // 0-9, ignores shift state
			if (digit == -1)
			{
				return;
			}
			int slot;
			if (mode == "shift")
			{
				if (!EInput.isShiftDown)
				{
					return; // plain digits stay on the native toolbelt
				}
				slot = digit;
			}
			else
			{
				slot = digit + (EInput.isShiftDown ? 10 : 0);
			}
			EInput.hotkey = -1; // native GetHotkeyDown ignores shift; keep the toolbelt out of it
			pending = new Plugin.Entry
			{
				name = "hotkey" + (slot + 1),
				run = delegate { MenuExtras.UseHotbarSlot(slot); },
				action = EAction.None
			};
			pendingFrame = Time.frameCount;
		}

		internal static void OnAdvInput()
		{
			if (pending == null || Time.frameCount != pendingFrame)
			{
				return;
			}
			Plugin.Entry entry = pending;
			pending = null;
			try
			{
				entry.run();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys handler '" + entry.name + "' failed: " + ex);
			}
			EInput.Consume();
		}

		private static void Debug(string msg)
		{
			if (Plugin.DebugLog)
			{
				Plugin.Log?.LogInfo("ElonaKeys: " + msg);
			}
		}

		private static void DebugGate(string gate)
		{
			if (Plugin.DebugLog && Time.unscaledTime - lastDebugLog > 1f && Input.anyKeyDown)
			{
				lastDebugLog = Time.unscaledTime;
				Plugin.Log?.LogInfo("ElonaKeys: key ignored, gate=" + gate);
			}
		}
	}

	[HarmonyPatch(typeof(EInput), nameof(EInput.Update))]
	internal static class Patch_EInputUpdate
	{
		private static void Postfix()
		{
			InputRouter.OnEInputUpdated();
		}
	}

	[HarmonyPatch(typeof(AM_Adv), "_OnUpdateInput")]
	internal static class Patch_AdvInput
	{
		private static void Prefix()
		{
			InputRouter.OnAdvInput();
		}
	}

	[HarmonyPatch(typeof(ActionMode), "InputMovement")]
	internal static class Patch_InputMovement
	{
		private static bool Prefix()
		{
			return !DirPrompt.Handle();
		}
	}
}
