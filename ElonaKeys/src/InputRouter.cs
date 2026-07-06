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
		private static EAction queuedNative = EAction.None;

		// let handlers (e.g. the wheel's Fire) trigger a native EAction next frame
		public static void QueueNative(EAction a)
		{
			queuedNative = a;
		}

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
			if (EClass.core == null || !EClass.core.IsGameStarted || EClass.pc == null || EClass.ui == null || EClass.scene == null)
			{
				return;
			}
			if (RadialWheel.Active)
			{
				// the wheel owns the keyboard: no movement, no native actions
				RadialWheel.HandleKeys();
				EInput.axis = Vector2.zero;
				EInput.action = EAction.None;
				EInput.hotkey = -1;
				EInput.functionkey = -1;
				return;
			}
			if (TargetMode.Active)
			{
				// in-world targeting: fence movement and native actions
				TargetMode.HandleKeys();
				EInput.axis = Vector2.zero;
				EInput.action = EAction.None;
				EInput.hotkey = -1;
				return;
			}
			if (DirPrompt.Handle())
			{
				return; // direction prompt is eating input this frame
			}
			// quick-craft float window: ARROW keys navigate its recipe list
			// (PopupNav), not the player; WASD still walks
			if (EClass.ui.TopLayer is LayerCraftFloat
				&& (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
			{
				EInput.axis = Vector2.zero;
			}
			if (queuedNative != EAction.None)
			{
				EInput.action = queuedNative;
				queuedNative = EAction.None;
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
			// bump tracking: fresh direction press edges (for talk/use on bump)
			Vector2 axisNow = EInput.axis;
			bool bumpFresh = axisNow != Vector2.zero && prevBumpAxis == Vector2.zero;
			prevBumpAxis = axisNow;
			// what did the user press?
			Plugin.Entry entry = Plugin.FindPressed();
			int digitSlot = -1;
			if (entry == null)
			{
				digitSlot = DigitActivationSlot();
				if (digitSlot == -1)
				{
					if (bumpFresh)
					{
						TryBump(axisNow);
					}
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
			if (EClass.ui.TopLayer is LayerCraftFloat)
			{
				// quick-craft is non-modal but PopupNav owns z/x/arrows there
				Gate(entry, "quick craft window open");
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
				// shift+digit: fire the other quickslot page
				EInput.hotkey = -1; // native GetHotkeyDown ignores shift
				MenuExtras.UseOtherPageSlot(digitSlot);
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

		private static Vector2 prevBumpAxis;

		// Elona habits: walking into a friendly NPC talks to them; walking into a
		// solid interactable (quest board, gacha, fountain) uses it.
		private static void TryBump(Vector2 a)
		{
			if (EInput.isShiftDown || EInput.IsConsumed)
			{
				return; // shift+move = run / tool use
			}
			if (!Plugin.BumpToTalk && !Plugin.BumpToUse)
			{
				return;
			}
			ActionMode am = EClass.scene.actionMode;
			if (!(am is AM_Adv) || am is AM_ADV_Target || !EClass.pc.currentZone.IsActiveZone || EClass.ui.BlockActions)
			{
				return;
			}
			if (EClass.ui.contextMenu != null && EClass.ui.contextMenu.isActive)
			{
				return;
			}
			Point p = EClass.pc.pos.Copy();
			p.x += Math.Sign(a.x);
			p.z += Math.Sign(a.y);
			if (!p.IsValid)
			{
				return;
			}
			if (Plugin.BumpToTalk)
			{
				foreach (Chara c in p.Charas)
				{
					if (c != EClass.pc && !c.IsPCParty && !c.IsHostile() && EClass.pc.CanSee(c))
					{
						EInput.axis = Vector2.zero; // talk instead of shoving past
						ACT.Chat.Perform(EClass.pc, c, p);
						return;
					}
				}
			}
			if (Plugin.BumpToUse && p.IsBlocked)
			{
				ActPlan plan = Handlers.BuildPlan(p);
				if (plan.HasAct && !plan.list[0].act.IsHostileAct)
				{
					EInput.axis = Vector2.zero;
					if (plan.list[0].Perform())
					{
						EClass.player.EndTurn();
					}
				}
			}
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
		// shift+digit fires the NON-displayed quickslot page; plain digits are
		// fully native (WidgetCurrentTool). Returns slot 0-8 or -1.
		private static int DigitActivationSlot()
		{
			if (Plugin.ActivateHotkeys == "off" || !EInput.isShiftDown)
			{
				return -1;
			}
			int digit = MenuExtras.GetDigitDown();
			if (digit == -1 || digit >= 9)
			{
				return -1;
			}
			return digit;
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
			return !DirPrompt.Active; // no walking while a direction prompt waits
		}
	}

	// Every container-open route (mouse, bump, space, interact) funnels into
	// TraitContainer.Open: redirect to the keyboard take/put menu. TryOpen's
	// lockpick branch and Open's surprise-package (mimic) branch stay native.
	[HarmonyPatch(typeof(TraitContainer), nameof(TraitContainer.Open))]
	internal static class Patch_ContainerOpen
	{
		private static bool Prefix(TraitContainer __instance)
		{
			if (!Plugin.KeyboardContainers || __instance.owner == null || __instance.HasChara || !(__instance.owner is Thing))
			{
				return true;
			}
			try
			{
				Handlers.ContainerMenu(__instance.owner.Thing);
				return false;
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys container redirect failed: " + ex);
				return true;
			}
		}
	}

	// Item processors (mill, seed maker, grindstone...) open the native drag
	// grid; put our ingredient picker on top so the whole flow stays keyboard.
	[HarmonyPatch(typeof(LayerDragGrid), nameof(LayerDragGrid.CreateCraft))]
	internal static class Patch_CrafterOpen
	{
		private static void Postfix(TraitCrafter crafter)
		{
			if (!Plugin.KeyboardCrafters || crafter == null)
			{
				return;
			}
			try
			{
				Handlers.CrafterIngredientMenu(crafter, 0);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys crafter menu failed: " + ex);
			}
		}
	}

	// While in-world targeting is active, feed the selected target into the
	// game's mouse-target so hover UIs (WidgetMouseover, BetterHoverText...)
	// display it exactly as if the mouse were on it.
	[HarmonyPatch(typeof(ActionMode), nameof(ActionMode.UpdateInput))]
	internal static class Patch_HoverFeed
	{
		private static void Postfix()
		{
			if (!TargetMode.Active)
			{
				return;
			}
			try
			{
				Point p = TargetMode.SelectedPos();
				if (p != null)
				{
					PointTarget mt = EClass.scene.mouseTarget;
					bool wasMouse = mt.mouse;
					mt.mouse = false; // skip the pointer-over-UI bailout
					mt.Update(p);
					mt.mouse = wasMouse;
					if ((bool)WidgetMouseover.Instance)
					{
						WidgetMouseover.Instance.Refresh();
					}
				}
			}
			catch
			{
				// cosmetic only
			}
		}
	}
}
