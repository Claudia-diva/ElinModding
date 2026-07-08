using System;
using UnityEngine;

namespace ElonaKeys
{
	// A single key binding: base key + exact shift state. Ctrl/Alt always cancel binds.
	public struct Keybind
	{
		public KeyCode key;
		public bool shift;

		public bool IsEmpty => key == KeyCode.None;

		public bool IsDown()
		{
			if (key == KeyCode.None)
			{
				return false;
			}
			if (EInput.isCtrlDown || EInput.isAltDown)
			{
				return false;
			}
			if (EInput.isShiftDown != shift)
			{
				return false;
			}
			return Input.GetKeyDown(key);
		}

		// physically held right now (same modifier rules as IsDown)
		public bool IsHeld()
		{
			if (key == KeyCode.None)
			{
				return false;
			}
			if (EInput.isCtrlDown || EInput.isAltDown)
			{
				return false;
			}
			if (EInput.isShiftDown != shift)
			{
				return false;
			}
			return Input.GetKey(key);
		}

		// Accepts a comma-separated list of binds: "g, keypad0".
		public static System.Collections.Generic.List<Keybind> ParseList(string csv)
		{
			var list = new System.Collections.Generic.List<Keybind>();
			if (string.IsNullOrWhiteSpace(csv))
			{
				return list;
			}
			foreach (string part in csv.Split(','))
			{
				Keybind b = Parse(part);
				if (!b.IsEmpty)
				{
					list.Add(b);
				}
			}
			return list;
		}

		// Accepts "e", "shift+t", "period", "shift+comma", "none".
		public static Keybind Parse(string s)
		{
			Keybind bind = default(Keybind);
			if (string.IsNullOrWhiteSpace(s))
			{
				return bind;
			}
			foreach (string rawPart in s.Split('+'))
			{
				string part = rawPart.Trim();
				if (part.Length == 0)
				{
					continue;
				}
				if (part.Equals("shift", StringComparison.OrdinalIgnoreCase))
				{
					bind.shift = true;
					continue;
				}
				if (part.Equals("none", StringComparison.OrdinalIgnoreCase))
				{
					bind.key = KeyCode.None;
					continue;
				}
				if (part.Length == 1 && part[0] >= 'a' && part[0] <= 'z')
				{
					part = part.ToUpperInvariant();
				}
				if (Enum.TryParse<KeyCode>(part, ignoreCase: true, out var parsed))
				{
					bind.key = parsed;
				}
				else
				{
					Plugin.Log?.LogWarning("ElonaKeys: cannot parse key '" + part + "' in '" + s + "'");
				}
			}
			return bind;
		}

		public override string ToString()
		{
			if (key == KeyCode.None)
			{
				return "none";
			}
			return (shift ? "shift+" : "") + key;
		}
	}
}
