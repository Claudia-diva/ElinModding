using System;
using UnityEngine;

namespace ElonaKeys
{
	// "Which direction?" prompt, Elona style. While active it swallows movement
	// input (hooked from ActionMode.InputMovement) and waits for an axis press,
	// the wait key (self, when allowed), or cancel.
	public static class DirPrompt
	{
		private static Action<Point> onDir;
		private static bool allowSelf;

		public static bool Active => onDir != null;

		public static void Ask(string prompt, bool self, Action<Point> action)
		{
			onDir = action;
			allowSelf = self;
			Msg.SayRaw(prompt);
		}

		public static void Cancel()
		{
			onDir = null;
		}

		// Returns true while the prompt is consuming input (caller should skip InputMovement).
		public static bool Handle()
		{
			if (onDir == null)
			{
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
