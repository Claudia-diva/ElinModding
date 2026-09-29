using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ImmersionEnglish
{
	// English labels for the workshop mod 沉浸增强 (Immersion Enhancement,
	// qf.elin.immersion_enhancement by 戏三子, MIT). That mod hardcodes its
	// UI text in Chinese, so its block of the Graphics config reads Chinese
	// whatever the game language is.
	//
	// This does NOT fork it: it rewrites the label strings on their way into
	// the UI, keyed by the stable English ids the mod already passes next to
	// every label (AddToggle(button, "CellRgbLighting", "逐格 RGB 灯光增强", ...)).
	// The mod keeps updating from the workshop; if a signature ever changes,
	// the patch drops out with a log line and their UI simply stays Chinese.
	[BepInPlugin(Guid, Name, Version)]
	[BepInDependency(TargetGuid, BepInDependency.DependencyFlags.SoftDependency)]
	public class Plugin : BaseUnityPlugin
	{
		public const string Guid = "elin.immersionenglish";
		public const string Name = "Immersion Enhancement English";
		public const string Version = "1.0.0";
		private const string TargetGuid = "qf.elin.immersion_enhancement";
		private const string ViewType = "Qf.ImmersionEnhancement.GameGraphicsSettings+SettingsView";

		internal static ManualLogSource Log;
		private static ConfigEntry<bool> relabel; // not 'enabled': Behaviour has that
		private static ConfigEntry<bool> keepChineseInChinese;

		internal static bool Active
		{
			get
			{
				if (relabel == null || !relabel.Value)
				{
					return false;
				}
				if (keepChineseInChinese == null || !keepChineseInChinese.Value)
				{
					return true;
				}
				// a Chinese game stays Chinese: this is a translation for
				// everyone else, not a replacement of the author's text
				try
				{
					string code = Lang.langCode;
					return string.IsNullOrEmpty(code) || !code.StartsWith("CN", StringComparison.OrdinalIgnoreCase);
				}
				catch
				{
					return true;
				}
			}
		}

		private void Awake()
		{
			Log = Logger;
			relabel = Config.Bind("General", "Enabled", true,
				"Relabel the Immersion Enhancement settings block in English.\nOff = leave the mod's own Chinese text alone.");
			keepChineseInChinese = Config.Bind("General", "KeepChineseInChineseGame", true,
				"Skip the relabeling while the game itself runs in Chinese.\nOff = always show the English labels.");

			Type view = AccessTools.TypeByName(ViewType);
			if (view == null)
			{
				// not installed, or renamed by an update: nothing to do
				Logger.LogInfo("Immersion Enhancement not found (" + ViewType + "); nothing to relabel.");
				return;
			}

			int patched = 0;
			Harmony harmony = new Harmony(Guid);
			patched += Patch(harmony, view, "AddToggle", nameof(Labels.RowPrefix));
			patched += Patch(harmony, view, "AddSlider", nameof(Labels.RowPrefix));
			patched += Patch(harmony, view, "CloneGroup", nameof(Labels.GroupPrefix));
			patched += Patch(harmony, view, "CreateResetPresetButton", nameof(Labels.ResetButtonPostfix), postfix: true);
			Logger.LogInfo(Name + " " + Version + " loaded, " + patched + "/4 label hooks active.");
		}

		private int Patch(Harmony harmony, Type view, string method, string patch, bool postfix = false)
		{
			try
			{
				MethodInfo target = AccessTools.Method(view, method);
				if (target == null)
				{
					Logger.LogWarning("Immersion Enhancement changed: " + method + " is gone, that part stays Chinese.");
					return 0;
				}
				HarmonyMethod hm = new HarmonyMethod(typeof(Labels), patch);
				harmony.Patch(target, postfix ? null : hm, postfix ? hm : null);
				return 1;
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Could not hook " + method + ", that part stays Chinese: " + ex.Message);
				return 0;
			}
		}
	}

	internal static class Labels
	{
		// keyed by the id the mod passes beside each label, which is already
		// English and stable across their releases
		private static readonly Dictionary<string, string> Rows = new Dictionary<string, string>
		{
			// toggles
			{ "Enabled", "Enable mod" },
			{ "AdaptiveEnvironment", "Adaptive environment (outdoor / indoor / dungeon)" },
			{ "NaturalLighting", "Day-night and weather curves" },
			{ "WhiteLight", "White light" },
			{ "AutomaticLightTone", "Shift color tone with the time of day" },
			{ "PreserveVanillaLocalLights", "Keep vanilla lights outdoors / indoors" },
			{ "PreserveVanillaDungeonLights", "Keep vanilla lights in dungeons" },
			{ "CellRgbLighting", "Per-tile RGB lighting boost" },
			{ "WallOcclusion", "Wall sight-blocking boost" },
			{ "Bloom", "Bloom" },
			{ "LutColor", "LUT and color correction" },
			{ "Fog", "Fog" },
			{ "Vignette", "Vignette" },
			{ "Godray", "Godray" },
			{ "DynamicShadows", "Outdoor sprite silhouette shadows" },
			{ "VegetationSway", "Plants and tree canopies sway" },
			{ "CloudShadows", "Drifting cloud shadows" },
			{ "SnowBrightnessSuppression", "Hold down snow brightness" },
			{ "WhiteHighlightSuppression", "Hold down blown-out white highlights" },
			{ "NightBrightnessEnhancement", "Brighten nights" },
			{ "NightColorCorrection", "Fix the night color cast" },
			{ "NightMoonlight", "Moonlight at night" },
			{ "DreamyNight", "Dreamlike nights" },
			{ "AllObjectShadows", "Shadows on everything (experimental)" },
			{ "AllEnvironmentShadows", "Shadows in every environment" },
			// sliders
			{ "LightingIntensity", "Light and shadow strength" },
			{ "LocalLightIntensity", "Local and carried light sources" },
			{ "ColorTone", "Color tone (cool / warm)" },
			{ "ShadowIntensity", "Shadow strength" },
			{ "BloomIntensity", "Bloom strength" },
			{ "FogIntensity", "Fog strength" },
			{ "VignetteIntensity", "Vignette strength" },
			{ "GodrayIntensity", "Godray strength" },
			{ "WindIntensity", "Sway strength" },
			{ "CloudShadowChance", "Cloud shadow chance" },
			{ "SnowBrightnessSuppressionIntensity", "Snow suppression strength" },
			{ "WhiteHighlightSuppressionIntensity", "White highlight suppression strength" },
			{ "FireflyAmount", "Firefly count" },
			{ "FireflySize", "Firefly size" },
			{ "NightBrightnessEnhancementIntensity", "Night brightening strength" },
			{ "NightColorCorrectionIntensity", "Night color fix strength" }
		};

		// the two section headers, keyed by the object name the mod gives
		// each cloned group (the titles themselves are Chinese literals)
		private static readonly Dictionary<string, string> Groups = new Dictionary<string, string>
		{
			{ "ImmersionEnhancement.ToggleGroup", "Immersion Enhancement" },
			{ "ImmersionEnhancement.SliderGroup", "Immersion Parameters" }
		};

		// AddToggle(button, id, label, entry, afterChange)
		// AddSlider(slider, id, label, entry, minimum, maximum, formatter)
		// Harmony binds these by PARAMETER NAME, so one prefix serves both.
		internal static void RowPrefix(string id, ref string label)
		{
			if (!Plugin.Active || id == null)
			{
				return;
			}
			if (Rows.TryGetValue(id, out string en))
			{
				label = en;
			}
		}

		// CloneGroup(source, parent, objectName, title)
		internal static void GroupPrefix(string objectName, ref string title)
		{
			if (!Plugin.Active || objectName == null)
			{
				return;
			}
			if (Groups.TryGetValue(objectName, out string en))
			{
				title = en;
			}
		}

		// the bottom "restore preset" button sets its text inside the method,
		// so fix it afterwards through the private field the mod stores it in
		internal static void ResetButtonPostfix(object __instance)
		{
			if (!Plugin.Active || __instance == null)
			{
				return;
			}
			try
			{
				UIButton button = Traverse.Create(__instance).Field("_resetPresetButton").GetValue<UIButton>();
				if (button != null && button.mainText != null)
				{
					button.mainText.lang = string.Empty;
					button.mainText.SetText("Restore immersion preset");
				}
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogWarning("Reset-preset button text left as-is: " + ex.Message);
			}
		}
	}
}
