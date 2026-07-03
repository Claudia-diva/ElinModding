using System;
using HarmonyLib;
using UnityEngine;

namespace ElonaKeys
{
	// Single-stage input interception in an EInput.Update postfix: right after the
	// game maps keys to EInput.action and BEFORE ActionMode.UpdateInput consumes it.
	// Handlers run inline here (equivalent timing to an ActionMode.UpdateInput
	// prefix); gating that AM_Adv._OnUpdateInput would normally provide (game
	// started, adventure mode, active zone, no modal layer) is replicated explicitly
	// so nothing depends on which method the game decides to call this frame.
	// Every rejection is logged when DebugLog is on.
	public static class InputRouter
	{
		private static float lastLog;
		private static float lastError;

		internal static void OnEInputUpdated()
		{
			try
			{
				Process();
			}
			catch (Exception ex)
			{
				// never let an exception take the whole pipeline down
				if (Time.unscaledTime - lastError > 5f)
				{
					lastError = Time.unscaledTime;
					Plugin.Log?.LogError("ElonaKeys input failed: " + ex);
				}
			}
		}

		private static void Process()
		{
			if (DirPrompt.Active || EClass.core == null || !EClass.core.IsGameStarted || EClass.pc == null || EClass.ui == null || EClass.scene == null)
			{
				return;
			}
			if (EInput.isInputFieldActive || EInput.isCtrlDown || EInput.isAltDown)
			{
				return;
			}
			// swallow keys the user asked us to hide from the game entirely
			foreach (Keybind sb in Plugin.SuppressBinds)
			{
				if (sb.IsDown())
				{
					EInput.action = EAction.None;
					EInput.Consume();
					return;
				}
			}
			// shift+direction with a held tool = use it on the adjacent tile
			if (ShiftDirTool())
			{
				return;
			}
			// what did the user press?
			Plugin.Entry entry = Plugin.FindPressed();
			int digitSlot = -1;
			if (entry == null)
			{
				digitSlot = DigitActivationSlot();
				if (digitSlot == -1)
				{
					return; // nothing of ours pressed
				}
			}
			// gates, each logged so a dead key can always be diagnosed
			if (EInput.IsConsumed)
			{
				Gate(entry, "EInput.IsConsumed(skipFrame)");
				return;
			}
			ActionMode am = EClass.scene.actionMode;
			if (!(am is AM_Adv))
			{
				Gate(entry, "actionMode=" + (am != null ? am.GetType().Name : "null"));
				return;
			}
			if (am is AM_ADV_Target)
			{
				Gate(entry, "targeting mode");
				return;
			}
			if (!EClass.pc.currentZone.IsActiveZone)
			{
				Gate(entry, "zone not active");
				return;
			}
			if (EClass.ui.BlockActions)
			{
				Gate(entry, "modal layer open (ui.BlockActions)");
				return;
			}
			if (EClass.ui.contextMenu != null && EClass.ui.contextMenu.isActive)
			{
				Gate(entry, "context menu open");
				return;
			}
			EAction current = EInput.action;
			// without override the game's own keys win (ShowGrid = hardcoded 'g', always overridable)
			if (!Plugin.OverrideGameKeys && current != EAction.None && current != EAction.ShowGrid)
			{
				Gate(entry, "native action wins: " + current);
				return;
			}
			if (entry == null)
			{
				// digit hotkey activation
				EInput.hotkey = -1; // keep the native toolbelt out of it (it ignores shift)
				MenuExtras.UseHotbarSlot(digitSlot);
				EInput.Consume();
				return;
			}
			if (entry.action != EAction.None)
			{
				EInput.action = entry.action; // native passthrough (Fire)
				Debug("passthrough " + entry.name + " -> " + entry.action);
				return;
			}
			EInput.action = EAction.None; // suppress whatever the game mapped to this key
			Debug("run " + entry.name);
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

		// While holding a tool, shift+direction applies it to the adjacent tile
		// (same act the drag-right-click designation would use). Returns true when
		// it consumed the axis this frame.
		private static bool ShiftDirTool()
		{
			if (!Plugin.ShiftDirTool || !EInput.isShiftDown || EInput.axis == Vector2.zero)
			{
				return false;
			}
			Thing held = EClass.pc.held as Thing;
			if (held == null || !(held.trait is TraitTool))
			{
				return false;
			}
			if (!(EClass.scene.actionMode is AM_Adv) || EClass.ui.BlockActions || !EClass.pc.currentZone.IsActiveZone)
			{
				return false;
			}
			Point p = EClass.pc.pos.Copy();
			p.x += (int)EInput.axis.x;
			p.z += (int)EInput.axis.y;
			if (!p.IsValid)
			{
				return false;
			}
			ActPlan plan = Handlers.BuildPlan(p, ActInput.RightMouse);
			if (!plan.HasAct || plan.list[0].act is ActMelee)
			{
				return false; // nothing tool-ish there: let shift+move stay running
			}
			EInput.axis = Vector2.zero; // no walking into it
			if (EClass.pc.HasNoGoal)
			{
				bool performed = plan.GetAction()();
				if (performed && plan.WillEndTurn)
				{
					EClass.player.EndTurn();
				}
			}
			return true;
		}

		// slot 0-19 to fire, or -1
		private static int DigitActivationSlot()
		{
			string mode = Plugin.ActivateHotkeys;
			if (mode != "shift" && mode != "plain")
			{
				return -1;
			}
			int digit = MenuExtras.GetDigitDown();
			if (digit == -1)
			{
				return -1;
			}
			if (mode == "shift")
			{
				return EInput.isShiftDown ? digit : -1; // plain digits stay on the native toolbelt
			}
			return digit + (EInput.isShiftDown ? 10 : 0);
		}

		private static void Gate(Plugin.Entry entry, string reason)
		{
			if (Plugin.DebugLog && Time.unscaledTime - lastLog > 0.25f)
			{
				lastLog = Time.unscaledTime;
				Plugin.Log?.LogInfo("ElonaKeys: '" + (entry?.name ?? "hotkey-digit") + "' ignored, gate=" + reason);
			}
		}

		private static void Debug(string msg)
		{
			if (Plugin.DebugLog)
			{
				Plugin.Log?.LogInfo("ElonaKeys: " + msg);
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

	[HarmonyPatch(typeof(ActionMode), "InputMovement")]
	internal static class Patch_InputMovement
	{
		private static bool Prefix()
		{
			return !DirPrompt.Handle();
		}
	}
}
