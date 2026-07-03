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

		private const float Timeout = 15f;

		public static bool Active => onDir != null;

		public static void Ask(string prompt, bool self, Action<Point> action)
		{
			onDir = action;
			allowSelf = self;
			started = Time.unscaledTime;
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
				Msg.SayRaw("(cancelled)");
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
			if (axis != Vector2.zero)
			{
				Point p = EClass.pc.pos.Copy();
				p.x += (int)axis.x;
				p.z += (int)axis.y;
				Fire(p);
				return true;
			}
			// still waiting; block movement this frame
			EInput.axis = Vector2.zero;
			return true;
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
