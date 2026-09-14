using AbyssMod.UI;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace AbyssMod.Patches;

[HarmonyPatch]
internal static class SettingsMenuRaycastPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(EventSystem), nameof(EventSystem.RaycastAll))]
    internal static void FilterResults(Il2CppSystem.Collections.Generic.List<RaycastResult> __1)
    {
        if (!MenuMouseIsolation.BlocksGame)
            return;
        for (int i = __1.Count - 1; i >= 0; i--)
            if (!SettingsMenuController.IsMenuTarget(__1[i].gameObject))
                __1.RemoveAt(i);
    }
}
