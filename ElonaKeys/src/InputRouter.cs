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
		// movement-interrupt state (see the interrupt block)
		private static float taskKillUntil;
		private static Vector2 prevInterruptAxis;
		// held-direction bump retrigger (tunneling cadence)
		private static float nextHeldBump;

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
			// the pause toggle comes FIRST: while paused, NOTHING of ours runs,
			// not even the fences (arrow/Tab/Escape all pure native)
			foreach (Keybind kb in Plugin.SuppressAllBinds)
			{
				if (kb.IsDown())
				{
					suppressVanilla = !suppressVanilla;
					if (suppressVanilla)
					{
						MenuSystem.Close();
						TargetMode.Cancel();
						TargetMode.HideLockHud();
						PopupNav.ClearInvNav();
					}
					Msg.SayRaw(suppressVanilla
						? EKLang.F("{0} PAUSED: pure native Elin windows and keys. ({1} to resume)", Plugin.Name, kb)
						: EKLang.F("{0} resumed.", Plugin.Name));
					EInput.Consume();
					return;
				}
			}
			if (suppressVanilla)
			{
				return;
			}
			// the lock readout stays current every frame (cheap string bail)
			TargetMode.RefreshLockHud();
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
			// while PopupNav owns an inventory window's tabs, z/x are ITS keys:
			// without this the same press also fired the Wheel/LastMenu binds
			// (the Tab double-fire bug class)
			if (PopupNav.InvNavActive && (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X)))
			{
				EInput.action = EAction.None;
				return;
			}
			// MOVEMENT INTERRUPTS EVERYTHING (user law): a FRESH direction
			// press cancels the running action — mining, chopping, hauling,
			// cart rides — and opens a short kill-window so auto-repeat mods
			// cannot requeue the next task under the player's feet. FRESH is
			// the key part: bump-mining starts by HOLDING a direction into
			// the wall, so a key already held when the task began must not
			// cancel it.
			bool freshMove = EInput.axis != Vector2.zero && EInput.axis != prevInterruptAxis;
			prevInterruptAxis = EInput.axis;
			// Walking itself is not an action to interrupt. Keyboard walking is ONE
			// GoalManualMove that reads the current direction every step, so a turn
			// arrives here as a "fresh" axis. Cancelling it made every direction
			// change kill the walk and restart it the next frame: a hitch per turn.
			if (freshMove && !MenuSystem.Alive
				&& EClass.pc.ai != null && EClass.pc.ai.IsRunning && !EClass.pc.HasNoGoal
				&& !(EClass.pc.ai is GoalManualMove))
			{
				EClass.pc.ai.TryCancel(EClass.pc);
				taskKillUntil = Time.unscaledTime + 0.5f;
			}
			// while a bump-started task works the wall, the STILL-HELD key must
			// not feed the game's wall avoidance (it slides the character along
			// the face, abandoning the dig): held-from-start parks; only a
			// fresh press (above) interrupts
			if (!freshMove && EInput.axis != Vector2.zero
				&& EClass.pc.ai is Task && EClass.pc.ai.IsRunning && !EClass.pc.HasNoGoal)
			{
				EInput.axis = Vector2.zero;
			}
			// the kill-window: keep swatting re-queued TASKS briefly after an
			// interrupt, so one press stops an auto-repeat CHAIN, not one link
			if (taskKillUntil > Time.unscaledTime
				&& EClass.pc.ai is Task && EClass.pc.ai.IsRunning && !EClass.pc.HasNoGoal)
			{
				EClass.pc.ai.TryCancel(EClass.pc);
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
				taskKillUntil = Time.unscaledTime + 0.5f;
				Msg.SayRaw(EKLang.T("Action canceled."));
				EInput.action = EAction.None;
				EInput.Consume();
				return;
			}
			// Escape closes the WHOLE window stack at once (ours and native);
			// shift stays the one-step back key. Conversations and yes/no
			// dialogs keep their native Escape handling.
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				Layer top = EClass.ui.TopLayer;
				if ((MenuSystem.Alive || top != null) && !(top is LayerDrama) && !(top is Dialog))
				{
					if (MenuSystem.Alive)
					{
						MenuSystem.Close();
					}
					try
					{
						EClass.ui.CloseLayers();
					}
					catch
					{
					}
					PopupNav.ClearInvNav();
					EInput.action = EAction.None;
					EInput.Consume();
					return;
				}
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
			// (the pause toggle and its early return moved to the TOP of
			// Process: while paused even the fences must not run)
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
			// keypad0 INSIDE a menu = act with the whole stack (drop all...),
			// keypadMultiply = toggle the important mark: the GetAll and
			// Target world binds must not also fire
			if (MenuSystem.Alive && entry != null && (entry.name == "GetAll" || entry.name == "Target"))
			{
				return;
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
						TryBump(axisNow, fresh: true);
					}
					else if (axisNow != Vector2.zero && EClass.pc.HasNoGoal && Time.unscaledTime >= nextHeldBump)
					{
						// HELD direction into a wall: the walk there consumed
						// the fresh edge, and the game's wall avoidance would
						// slide the character along the face instead of
						// bumping. A held key keeps working the tile — and
						// each broken wall chains the next one (tunneling).
						nextHeldBump = Time.unscaledTime + 0.25f;
						TryBump(axisNow, fresh: false);
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
		// solid interactable (quest board, gacha, fountain) uses it. fresh =
		// a new keypress; held keys only re-bump (tunneling), never re-talk.
		private static void TryBump(Vector2 a, bool fresh)
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
			if (Plugin.BumpToTalk && fresh)
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
				// (run into a tree with an axe = chop; wall with pickaxe = mine,
				// ore veins included). Always-run friendly: no shift needed.
				// TASKS like TaskMine are hostile-FLAGGED because they can be a
				// crime, not because they attack — allow them whenever the zone
				// says it's legal, so bump-mining works outside towns.
				if (EClass.player.currentHotItem is HotItemHeld)
				{
					ActPlan toolPlan = Handlers.BuildPlan(p, ActInput.RightMouse);
					bool safeAct = toolPlan.HasAct && !(toolPlan.list[0].act is ActMelee)
						&& (!toolPlan.list[0].act.IsHostileAct
							|| (toolPlan.list[0].act is Task && !EClass._zone.IsCrime(EClass.pc, toolPlan.list[0].act)));
					if (safeAct)
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

	// every PC pickup records recency: feeds the "recent" sort and the (new)
	// tag ("I picked up random junk and need to find it to throw it"). The
	// RESULT is noted, not the argument — a stack merge moves the recency
	// onto the merged stack.
	[HarmonyPatch(typeof(Chara), nameof(Chara.Pick))]
	internal static class Patch_PickLog
	{
		private static void Postfix(Chara __instance, Thing __result)
		{
			if (__instance != null && __instance.IsPC && __result != null)
			{
				Handlers.PickLog.Note(__result);
			}
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
			if (InputRouter.VanillaLocked || Handlers.containerRedirectBypass || !Plugin.KeyboardContainers || __instance.owner == null || __instance.HasChara || !(__instance.owner is Thing))
			{
				return true; // paused/bypassed = the normal Elin window
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

	// EVERY "pick an item for X" flow funnels through LayerDragGrid.Create:
	// altar offerings, dip/blend, effect scrolls (identify, uncurse, enchant,
	// change material...), wizard services, bank deposit, tax box, harvest
	// chest, gacha, recycle bin, gene machine, mod kit, refuel. The native
	// grid opens a float backpack OUTSIDE the main layer stack, so no nav
	// ever reached it — the ElonaKeys item menu takes over instead.
	[HarmonyPatch(typeof(LayerDragGrid), nameof(LayerDragGrid.Create))]
	internal static class Patch_DragletOpen
	{
		private static void Prefix(out bool __state)
		{
			bool open = false;
			try
			{
				open = LayerInventory.GetPCLayer() != null;
			}
			catch
			{
			}
			__state = open; // was a player float inventory already open?
		}

		private static void Postfix(LayerDragGrid __result, InvOwnerDraglet owner, bool __state)
		{
			if (InputRouter.VanillaLocked || !Plugin.KeyboardContainers || owner == null || __result == null)
			{
				return;
			}
			if (owner is InvOwnerCraft)
			{
				return; // crafters have their own ingredient menu (CreateCraft postfix)
			}
			LayerDragGrid grid = __result;
			bool hadInv = __state;
			EClass.core.WaitForEndOfFrame(delegate
			{
				try
				{
					Handlers.HideTradeLayer(grid);
					if (!hadInv)
					{
						// the grid auto-opened this backpack: it's part of the
						// flow, hide it too (pre-existing windows stay put)
						LayerInventory pcl = LayerInventory.GetPCLayer();
						if (pcl != null)
						{
							Handlers.HideTradeLayer(pcl);
						}
					}
					Handlers.DragletMenu(owner);
				}
				catch (Exception ex)
				{
					Plugin.Log?.LogWarning("ElonaKeys draglet menu open failed: " + ex);
					Handlers.CloseShopLayers(); // hidden pooled layers must never linger
				}
			});
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
			if (EClass.ui != null && EClass.ui.TopLayer is LayerDrama)
			{
				// custom-drama "trade" choices AddLayer() the result and would
				// NRE on our null: let those stay native
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
			if (InputRouter.VanillaLocked || !Plugin.KeyboardShops)
			{
				return; // paused or disabled: the vanilla shop window
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
					Handlers.CloseShopLayers(); // a hidden pooled layer must never linger
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
