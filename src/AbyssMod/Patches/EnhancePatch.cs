using Absf;
using Absf.Api;
using AbyssMod.Services;
using HarmonyLib;
using Il2CppSystem.Threading;
using Project.Notice;
using Project.Novel;
using UnityEngine.Networking;

namespace AbyssMod.Patches;

/// <summary>
/// 游戏通用增强：关闭动态马赛克、音量警告、标题动画、语音中断控制、网络超时。
/// </summary>
[HarmonyPatch]
internal static class EnhancePatch
{
    private static int _allowStopVoiceCount;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelLive2DObject), nameof(NovelLive2DObject.Initialize))]
    public static void ConfigureLive2DDrawables(NovelLive2DObject __instance)
    {
        if (Config.DynamicMosaic.Value && !Config.HideNovelGlasses.Value)
            return;

        var drawables = __instance.GetDrawables();
        if (drawables == null)
            return;

        foreach (var d in drawables)
        {
            if (
                !Config.DynamicMosaic.Value
                && d.name.StartsWith("Mosaic", System.StringComparison.Ordinal)
            )
                d.gameObject.SetActive(false);
            if (
                Config.HideNovelGlasses.Value
                && d.name.Contains("glasses", System.StringComparison.OrdinalIgnoreCase)
            )
                d.gameObject.SetActive(false);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(SoundCautionPopupController),
        nameof(SoundCautionPopupController.SetupPopupEvent)
    )]
    public static bool DisableSoundCaution(SoundCautionPopupController __instance)
    {
        if (!Config.SoundCaution.Value)
        {
            __instance._onClickOk.Invoke();
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelSoundManager), nameof(NovelSoundManager.StopCategory))]
    public static bool CancelStoppingVoice(int nCategory, bool playFade)
    {
        if (Config.VoiceInterruption.Value || _allowStopVoiceCount > 0)
            return true;

        return nCategory != 2 || playFade;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelSoundManager), nameof(NovelSoundManager.PlaySound))]
    public static void StopVoiceBeforePlaying(NovelSoundManager __instance, SoundCategory category)
    {
        if (!Config.VoiceInterruption.Value && category == SoundCategory.Voice)
        {
            _allowStopVoiceCount++;
            try
            {
                __instance.StopCategory(2, false);
            }
            finally
            {
                _allowStopVoiceCount--;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Project.Title.TopView), nameof(Project.Title.TopView.PlayMovie))]
    public static void DisableTitleMovie(Project.Title.TopView __instance, CancellationToken ct)
    {
        if (!Config.TitleMovie.Value)
            __instance.MovieSkip(ct);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.timeout), MethodType.Setter)]
    public static void ChangeTimeoutLimit(ref int value)
    {
        value = 60;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelLive2DController), nameof(NovelLive2DController.Setup))]
    public static void BeginNovelLive2DScale(NovelLive2DController __instance) =>
        NovelLive2DScaleController.Track(__instance._canvasRoot);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelLive2DController), nameof(NovelLive2DController.Release))]
    public static void EndNovelLive2DScale(NovelLive2DController __instance) =>
        NovelLive2DScaleController.Release(__instance._canvasRoot);
}
