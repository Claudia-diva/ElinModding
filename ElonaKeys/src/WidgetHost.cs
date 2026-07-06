using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ElonaKeys
{
	// Registers this mod's overlays as REAL Elin widgets: they appear in the
	// game's widget manager ribbon with native toggle, drag, lock, anchor and
	// theme persistence. The game loads widgets from Resources prefabs only,
	// so metas are injected into setting.ui.widgetMetas (configs are created
	// and orphan-cleaned natively) and ActivateWidget is intercepted for our
	// ids to build the widget GameObjects at runtime.
	internal static class WidgetHost
	{
		internal const string IdFamilyBar = "EKFamilyBar";

		internal static bool Owns(string id)
		{
			return id == IdFamilyBar;
		}

		internal static void InjectMetas()
		{
			List<Widget.Meta> metas = EClass.setting.ui.widgetMetas;
			// same visibility modes as the hotbar
			GameMode mode = (GameMode)0;
			foreach (Widget.Meta m in metas)
			{
				if (m.id == "CurrentTool")
				{
					mode = m.mode;
					break;
				}
			}
			AddMeta(metas, IdFamilyBar, Plugin.FamilyBarVisible, mode);
		}

		private static void AddMeta(List<Widget.Meta> metas, string id, bool enabled, GameMode mode)
		{
			foreach (Widget.Meta m in metas)
			{
				if (m.id == id)
				{
					return; // theme resets re-run OnGameInstantiated; never duplicate
				}
			}
			metas.Add(new Widget.Meta
			{
				id = id,
				enabled = enabled,
				mode = mode
			});
		}

		// build root + dragPanel the way Widget.Activate/RefreshWidget expect
		internal static Widget Build(WidgetManager manager, string id)
		{
			GameObject root = new GameObject("Widget" + id);
			root.transform.SetParent(manager.transform, worldPositionStays: false);
			root.AddComponent<RectTransform>();
			root.AddComponent<SkinRoot>(); // binds to config.skin via Widget : ISkinRoot
			Widget w = root.AddComponent<WidgetEKFamilyBar>();
			GameObject dragGo = new GameObject("dragPanel");
			dragGo.transform.SetParent(root.transform, worldPositionStays: false);
			RectTransform drt = dragGo.AddComponent<RectTransform>();
			drt.anchorMin = Vector2.zero;
			drt.anchorMax = Vector2.one;
			drt.offsetMin = Vector2.zero;
			drt.offsetMax = Vector2.zero;
			Image g = dragGo.AddComponent<Image>();
			g.color = new Color(0f, 0f, 0f, 0.004f); // invisible but raycastable
			g.raycastTarget = true;
			UIDragPanel dp = dragGo.AddComponent<UIDragPanel>();
			dp.target = root.transform as RectTransform;
			dp.container = manager.transform as RectTransform;
			dp.clamp = true;
			w.dragPanel = dp;
			return w;
		}
	}

	[HarmonyPatch(typeof(WidgetManager), nameof(WidgetManager.OnGameInstantiated))]
	internal static class Patch_WidgetMetas
	{
		private static void Prefix()
		{
			try
			{
				WidgetHost.InjectMetas();
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys widget meta injection failed: " + ex);
			}
		}
	}

	[HarmonyPatch(typeof(WidgetManager), nameof(WidgetManager.ActivateWidget), new Type[] { typeof(string) })]
	internal static class Patch_WidgetActivate
	{
		private static bool Prefix(WidgetManager __instance, string id, ref Widget __result)
		{
			if (!WidgetHost.Owns(id))
			{
				return true;
			}
			try
			{
				Widget w = WidgetHost.Build(__instance, id);
				__instance.list.Add(w);
				w.Activate();
				__instance.RefreshWidget(w);
				if ((bool)LayerWidget.Instance)
				{
					w.OnManagerActivate();
				}
				__result = w;
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys widget activate failed (" + id + "): " + ex);
				__result = null;
			}
			return false;
		}
	}
}
