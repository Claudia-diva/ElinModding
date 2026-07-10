using System;
using UnityEngine;

namespace ElonaKeys
{
	// "Which direction?" prompt, Elona style. Processed every frame from the
	// EInput.Update postfix (never depends on which game method runs), with a
	// timeout so an abandoned prompt can never wedge the mod. Movement is
	// suppressed separately via the ActionMode.InputMovement prefix.
	public static class DirPrompt
	{
		private static Action<Point> onDir;
		private static bool allowSelf;
		private static float started;
		private static bool waitRelease;

		private const float Timeout = 15f;

		public static bool Active => onDir != null;

		public static void Ask(string prompt, bool self, Action<Point> action)
		{
			onDir = action;
			allowSelf = self;
			started = Time.unscaledTime;
			waitRelease = EInput.axis != Vector2.zero; // don't fire on a still-held movement key
			Msg.SayRaw(prompt);
		}

		public static void Cancel()
		{
			onDir = null;
		}

		// Runs every frame while active (from InputRouter). Returns true while the
		// prompt is consuming input.
		public static bool Handle()
		{
			if (onDir == null)
			{
				return false;
			}
			if (Time.unscaledTime - started > Timeout)
			{
				Cancel();
				Msg.SayRaw(EKLang.T("(cancelled)"));
				return false;
			}
			// a modal opening on top of the prompt orphans it: bail out
			// (small grace period: the menu that spawned us may still be closing)
			if (Time.unscaledTime - started > 0.5f && (EClass.ui == null || EClass.ui.BlockActions))
			{
				Cancel();
				return false;
			}
			if (Input.GetKeyDown(KeyCode.Escape) || EInput.rightMouse.down || EInput.leftMouse.down)
			{
				Cancel();
				EInput.action = EAction.None;
				EInput.Consume(consumeAxis: true);
				return true;
			}
			if (allowSelf && EInput.action == EAction.Wait)
			{
				Fire(EClass.pc.pos.Copy());
				return true;
			}
			Vector2 axis = EInput.axis;
			if (waitRelease)
			{
				if (axis == Vector2.zero)
				{
					waitRelease = false;
				}
				Swallow();
				HighlightNeighbors();
				return true;
			}
			if (axis != Vector2.zero)
			{
				Vector2 dir = Plugin.IsoAim ? IsoRotate(axis) : axis;
				Point p = EClass.pc.pos.Copy();
				p.x += (int)dir.x;
				p.z += (int)dir.y;
				Fire(p);
				return true;
			}
			// still waiting; the prompt is modal: no movement AND no native actions
			// (otherwise e.g. the native ability menu opens mid-bash-prompt)
			Swallow();
			HighlightNeighbors();
			return true;
		}

		// Elin renders isometric: the grid is rotated 45° from the screen, so a
		// "down" press should aim at the tile that LOOKS south (grid south-east).
		// One compass step of rotation maps screen direction to grid direction.
		internal static Vector2 IsoRotate(Vector2 v)
		{
			int x = Math.Sign(v.x);
			int y = Math.Sign(v.y);
			// rotate 45°: screen (x, y) -> grid (x - y, y + x), clamped to -1..1
			// checks: screen S (0,-1) -> grid (1,-1) which renders straight down;
			// screen E (1,0) -> grid (1,1); screen N (0,1) -> grid (-1,1)
			return new Vector2(Mathf.Clamp(x - y, -1, 1), Mathf.Clamp(y + x, -1, 1));
		}

		private static void Swallow()
		{
			EInput.axis = Vector2.zero;
			EInput.action = EAction.None;
			EInput.hotkey = -1;
			EInput.functionkey = -1;
		}

		private static void HighlightNeighbors()
		{
			try
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						if (dx == 0 && dz == 0 && !allowSelf)
						{
							continue;
						}
						Point p = EClass.pc.pos.Copy();
						p.x += dx;
						p.z += dz;
						if (p.IsValid)
						{
							p.SetHighlight(8);
						}
					}
				}
			}
			catch
			{
				// cosmetic only
			}
		}

		private static void Fire(Point p)
		{
			Action<Point> cb = onDir;
			onDir = null;
			EInput.action = EAction.None;
			EInput.Consume(consumeAxis: true);
			try
			{
				cb(p);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys direction action failed: " + ex);
			}
		}
	}
}
