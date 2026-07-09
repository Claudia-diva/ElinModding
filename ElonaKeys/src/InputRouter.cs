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
			// our menus own the keyboard: WASD must not walk "through" the
			// window (w on a spell row = Wide cast, not a step forward), and
			// native actions/hotbar digits stay out of the world. Escape-close
			// is unaffected (layers read Escape directly, not EInput.action).
			if (MenuSystem.Alive)
			{
				EInput.axis = Vector2.zero;
				EInput.action = EAction.None;
				EInput.hotkey = -1;
			}
			// quick-craft float window: ARROW keys navigate its recipe list
			// (PopupNav), not the player; WASD still walks
			if (EClass.ui.TopLayer is LayerCraftFloat
				&& (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
			{
				EInput.axis = Vector2.zero;
			}
			// Tab is ALSO a native Elin action (inventory): while our menus or any
			// modal window own it (family cycling, window tabs), neutralize the
			// native dispatch or closing our layer lets it toggle the inventory
			if (Input.GetKey(KeyCode.Tab) && (MenuSystem.Alive || EClass.ui.BlockActions || PopupNav.InvNavActive))
			{
				EInput.action = EAction.None;
			}
			// inventory/trade navigation: arrows walk the item grids, not the
			// player (same fence the quick-craft float window gets)
			if (PopupNav.InvNavActive
				&& (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
			{
				EInput.axis = Vector2.zero;
			}
			// Escape CANCELS the running action: crafting always (its window
			// hides itself mid-craft), and with no window open, any running
			// TASK — eating, processing, stealing, harvesting. HasNoGoal is
			// the gate: the idle AI (NoGoal) also "runs" constantly, and
			// consuming Escape for it blocked the system menu entirely.
			if (Input.GetKeyDown(KeyCode.Escape) && EClass.pc.ai != null && EClass.pc.ai.IsRunning && !EClass.pc.HasNoGoal
				&& (EClass.pc.ai is AI_UseCrafter || EClass.pc.ai is TaskCraft || EClass.ui.TopLayer == null))
			{
				EClass.pc.ai.TryCancel(EClass.pc);
				Msg.SayRaw("Action canceled.");
				EInput.action = EAction.None;
				EInput.Consume();
				return;
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
			// mod-pause toggle (default CapsLock), REPURPOSED by request: while
			// ON, ElonaKeys steps aside COMPLETELY — no mod keys, no window
			// redirects, no trade panes. Pure native Elin, the escape hatch
			// for anything the mod gets wrong.
			foreach (Keybind kb in Plugin.SuppressAllBinds)
			{
				if (kb.IsDown())
				{
					suppressVanilla = !suppressVanilla;
					if (suppressVanilla)
					{
						MenuSystem.Close();
						TargetMode.Cancel();
					}
					Msg.SayRaw(suppressVanilla
						? Plugin.Name + " PAUSED: pure native Elin windows and keys. (" + kb + " to resume)"
						: Plugin.Name + " resumed.");
					EInput.Consume();
					return;
				}
			}
			if (suppressVanilla)
			{
				return; // paused: everything native, nothing of ours runs
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
			// THE OverrideGameKeys promise, kept fully: the moment a pressed key
			// matches one of our binds, the native action is dead — even when a
			// gate below skips our handler (float windows, wrong mode...).
			// Previously suppression only happened after ALL gates passed, so
			// vanilla actions leaked through in exactly those in-between states.
			if (entry != null && Plugin.OverrideGameKeys && entry.action == EAction.None)
			{
				EInput.action = EAction.None;
			}
			// verb keys that double as movement (s/w/a/d) must not ALSO step
			// the character — for the WHOLE press, not just the down frame:
			// s on a collectable started the harvest, then walked south a few
			// frames later and cancelled it
			if (Plugin.OverrideGameKeys && Plugin.AnyBindHeld())
			{
				EInput.axis = Vector2.zero;
			}
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
				// shift+digit: fire the ElonaKeys quickbind registry
				EInput.hotkey = -1; // native GetHotkeyDown ignores shift
				Quickbind.Fire(digitSlot);
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
		private static bool suppressVanilla;

		internal static bool VanillaLocked => suppressVanilla;

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
				// no held-item acts first: bumping a door while holding an axe
				// should open it, not start chopping it
				ActPlan plan = Handlers.BuildPlan(p, ActInput.AllAction, includeHeld: false);
				if (plan.HasAct && !plan.list[0].act.IsHostileAct)
				{
					EInput.axis = Vector2.zero;
					if (plan.list[0].Perform())
					{
						EClass.player.EndTurn();
					}
					return;
				}
				// nothing else there: let the HELD TOOL work the tile on bump
				// (run into a tree with an axe = chop; wall with pickaxe = mine).
				// Always-run friendly: no shift needed.
				if (EClass.player.currentHotItem is HotItemHeld)
				{
					ActPlan toolPlan = Handlers.BuildPlan(p, ActInput.RightMouse);
					if (toolPlan.HasAct && !toolPlan.list[0].act.IsHostileAct && !(toolPlan.list[0].act is ActMelee))
					{
						if (Plugin.DebugLog)
						{
							Plugin.Log?.LogInfo("ElonaKeys: bump-tool " + toolPlan.list[0].act.GetType().Name);
						}
						EInput.axis = Vector2.zero;
						if (toolPlan.list[0].Perform())
						{
							EClass.player.EndTurn();
						}
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
			if (InputRouter.VanillaLocked || !Plugin.KeyboardContainers || __instance.owner == null || __instance.HasChara || !(__instance.owner is Thing))
			{
				return true; // paused = the normal Elin window
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
			if (InputRouter.VanillaLocked || !Plugin.KeyboardCrafters || crafter == null)
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

	// "Trade" with a party member/pet opens the vanilla ally-inventory window
	// (actTrade -> CreateContainer(chara)); party moves are free-form, so the
	// ElonaKeys two-pane menu replaces it entirely
	[HarmonyPatch(typeof(LayerInventory), nameof(LayerInventory.CreateContainer), new Type[] { typeof(Card) })]
	internal static class Patch_TradeOpen
	{
		private static bool Prefix(Card owner)
		{
			if (InputRouter.VanillaLocked || !Plugin.KeyboardContainers)
			{
				return true;
			}
			if (owner is Chara ch && ch.IsPCFaction && !ch.IsPC)
			{
				try
				{
					Handlers.TradeMenu(ch);
					return false; // no native window at all
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogError("ElonaKeys trade menu failed (native kept): " + ex);
					return true;
				}
			}
			return true;
		}
	}

	// An explicitly aimed cast may target ANYONE (classic Elona): while our
	// force-cast helpers run, enemy-only target types accept allies too. The
	// flag is only true inside CanForcePerform/ForceUse, so native aiming and
	// AI casting are untouched.
	[HarmonyPatch(typeof(TargetTypeEnemy), "get_CanOnlyTargetEnemy")]
	internal static class Patch_ForceFriendlyFire
	{
		private static bool Prefix(ref bool __result)
		{
			if (Handlers.ForceFriendlyFire)
			{
				__result = false;
				return false;
			}
			return true;
		}
	}

	// Tab's inventory toggle is HARDCODED raw input in ActionMode.UpdateInput
	// (ActionMode.cs:739), not an EInput action: block it at the destination.
	// Mouse clicks on the inventory button pass (no Tab keydown that frame).
	[HarmonyPatch(typeof(UI), nameof(UI.ToggleInventory), new Type[] { typeof(bool) })]
	internal static class Patch_ToggleInventory
	{
		private static bool Prefix()
		{
			if (InputRouter.VanillaLocked)
			{
				return true; // paused: Tab toggles the inventory natively
			}
			if (Input.GetKey(KeyCode.Tab)
				&& (MenuSystem.Alive || PopupNav.InvNavActive || (EClass.ui != null && EClass.ui.BlockActions)))
			{
				// InvNavActive: Tab hops trade panes, must not toggle the backpack
				return false;
			}
			return true;
		}
	}

	// A shopkeeper's window opens: bring up your backpack next to it so the
	// sell pane is one Tab away (Elona layout). The native buy/sell flow and
	// its checks (money, space, karma) are untouched.
	[HarmonyPatch(typeof(LayerInventory), nameof(LayerInventory.CreateBuy))]
	internal static class Patch_ShopOpen
	{
		private static void Postfix(LayerInventory __result)
		{
			if (InputRouter.VanillaLocked)
			{
				return;
			}
			// END OF FRAME, not now: the game calls
			// ui.AddLayer(LayerInventory.CreateBuy(...)), so at postfix time
			// the shop layer isn't in the UI yet — anything we open here ends
			// up UNDER it, and its window init overrides coordinate changes.
			LayerInventory shopLayer = __result;
			EClass.core.WaitForEndOfFrame(delegate
			{
				try
				{
					// hide IN PLACE, only the shop's own window; transactions
					// are direct now, so NO backpack pane is opened and no
					// pre-existing player window is ever touched
					Handlers.HideTradeLayer(shopLayer);
					// the ElonaKeys shop window: prices, letters, Tab
					// buy<->sell, spinner counts, direct transactions
					Handlers.ShopMenu();
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogWarning("ElonaKeys shop auto-open failed: " + ex);
				}
			});
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
