using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ElonaKeys
{
	// Translation table, gettext style: the ENGLISH string IS the key.
	// Translators drop LangMod/<langCode>/elonakeys.tsv into the mod
	// package (english<TAB>translation per line, \n for line breaks);
	// the game's language setting picks the file. Missing file or key =
	// English. LangMod/EN/elonakeys.tsv ships as the full template.
	internal static class EKLang
	{
		private static Dictionary<string, string> table;
		private static string loadedCode;

		// plain string
		internal static string T(string en)
		{
			try
			{
				Ensure();
				if (table != null && table.TryGetValue(en, out string tr) && !string.IsNullOrEmpty(tr))
				{
					return tr;
				}
			}
			catch
			{
			}
			return en;
		}

		// string.Format template ({0}, {1}...) so translations can reorder
		internal static string F(string en, params object[] args)
		{
			string t = T(en);
			try
			{
				return string.Format(t, args);
			}
			catch
			{
				try
				{
					return string.Format(en, args);
				}
				catch
				{
					return en;
				}
			}
		}

		private static void Ensure()
		{
			string code = "EN";
			try
			{
				if (!string.IsNullOrEmpty(Lang.langCode))
				{
					code = Lang.langCode;
				}
			}
			catch
			{
			}
			if (code == loadedCode)
			{
				return;
			}
			loadedCode = code;
			table = null;
			if (code == "EN")
			{
				return; // English lives in the code itself
			}
			try
			{
				string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string file = Path.Combine(dir, "LangMod", code, "elonakeys.tsv");
				if (!File.Exists(file))
				{
					return;
				}
				var t = new Dictionary<string, string>();
				foreach (string line in File.ReadAllLines(file))
				{
					if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
					{
						continue;
					}
					int tab = line.IndexOf('\t');
					if (tab > 0)
					{
						string k = line.Substring(0, tab).Replace("\\n", "\n");
						string v = line.Substring(tab + 1).Trim().Replace("\\n", "\n");
						if (v.Length > 0)
						{
							t[k] = v;
						}
					}
				}
				table = t;
				Plugin.Log?.LogInfo("ElonaKeys: " + t.Count + " translations loaded for " + code);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys translation load failed: " + ex.Message);
			}
		}
	}
}
