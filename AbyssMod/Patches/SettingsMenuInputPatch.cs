using AbyssMod.UI;
using HarmonyLib;
using Project;
using Project.Novel;

namespace AbyssMod.Patches;

// 只处理游戏层入口，不 hook UnityEngine.Input。
[HarmonyPatch]
internal static class SettingsMenuInputPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelInputComponent), nameof(NovelInputComponent.CallClick))]
    internal static bool AllowNovelInput() => !MenuMouseIsolation.BlocksGame;

    // 不拦取消/结束等回调，避免把已经按下的游戏操作留在未释放状态。
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelModelMessage), nameof(NovelModelMessage.Click))]
    internal static bool AllowMessageClick() => !MenuMouseIsolation.BlocksGame;

    [HarmonyPrefix, HarmonyPatch(typeof(NovelViewClick), nameof(NovelViewClick.OnLongClick))]
    internal static bool AllowLongClick() => !MenuMouseIsolation.BlocksGame;

    [HarmonyPrefix, HarmonyPatch(typeof(NovelViewClick), nameof(NovelViewClick.OnViewUpdate))]
    internal static bool AllowNovelViewInputUpdate() => !MenuMouseIsolation.BlocksGame;

    [
        HarmonyPrefix,
        HarmonyPatch(typeof(NovelInputComponent), nameof(NovelInputComponent.StartDownCoroutine))
    ]
    internal static bool AllowNewHoldTimer() => !MenuMouseIsolation.BlocksGame;

    [
        HarmonyPrefix,
        HarmonyPatch(
            typeof(NovelInputComponent),
            "UnityEngine_EventSystems_IPointerDownHandler_OnPointerDown"
        )
    ]
    internal static bool AllowNovelPointerDown() => !MenuMouseIsolation.BlocksGame;

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

    [HarmonyPrefix, HarmonyPatch(typeof(TouchEffectService), nameof(TouchEffectService.PlayEffect))]
    internal static bool AllowTouchEffect() => !MenuMouseIsolation.BlocksGame;

    [HarmonyPrefix, HarmonyPatch(typeof(NovelInputComponent), "PressCheck")]
    internal static bool AllowNovelPress() => !MenuMouseIsolation.BlocksGame;

    [HarmonyPrefix, HarmonyPatch(typeof(NovelInputComponent), "PressingCheck")]
    internal static bool AllowNovelPressing() => !MenuMouseIsolation.BlocksGame;

    [HarmonyPrefix, HarmonyPatch(typeof(NovelInputComponent), "RepeatCheck")]
    internal static bool AllowNovelRepeat() => !MenuMouseIsolation.BlocksGame;
}
