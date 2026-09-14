using AbyssMod.UI;
using HarmonyLib;
using Project;
using Project.Novel;

namespace AbyssMod.Patches;

// 只拦截稳定的游戏层入口，不 hook UnityEngine.Input，也不 patch NovelInputComponent
// 的长按热路径；剧情按钮由 MenuGameInputGuard 直接禁用并清理状态。
[HarmonyPatch]
internal static class SettingsMenuInputPatch
{
    [HarmonyPrefix, HarmonyPatch(typeof(NovelViewClick), nameof(NovelViewClick.OnViewUpdate))]
    internal static bool AllowNovelViewInputUpdate() => !MenuMouseIsolation.BlocksGame;

    [HarmonyPrefix, HarmonyPatch(typeof(InputService), nameof(InputService.OnUpdate))]
    internal static bool AllowGameInputUpdate(InputService __instance)
    {
        if (!MenuMouseIsolation.BlocksGame)
            return true;
        // 丢弃打开菜单前残留的按住/拖拽状态，不在关菜单后补发点击。
        __instance._inputState = InputState.None;
        __instance._holdEventTriggered = false;
        __instance._startTime = 0;
        return false;
    }
}
