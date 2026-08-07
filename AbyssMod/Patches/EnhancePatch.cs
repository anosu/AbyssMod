using System.Collections.Generic;
using Absf;
using Absf.Api;
using HarmonyLib;
using Il2CppSystem.Threading;
using Project.Nether.FloorSelection;
using Project.Notice;
using Project.Novel;
using UnityEngine;
using UnityEngine.Networking;

namespace AbyssMod.Patches;

/// <summary>
/// 游戏通用增强：关闭动态马赛克、音量警告、标题动画、语音中断控制、网络超时。
/// </summary>
[HarmonyPatch]
public static class EnhancePatch
{
    private const float NovelLive2DScaleSaveDelay = 1f;

    private static readonly Dictionary<Transform, Vector3> _novelLive2DOriginalScales = new();
    private static int _allowStopVoiceCount;
    private static float _novelLive2DScale = float.NaN;
    private static float _novelLive2DScaleSaveTime;
    private static bool _novelLive2DScaleSavePending;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelLive2DObject), nameof(NovelLive2DObject.Initialize))]
    public static void DisableMosaic(NovelLive2DObject __instance)
    {
        if (Config.DynamicMosaic.Value)
            return;

        var drawables = __instance.GetDrawables();
        if (drawables == null)
            return;

        foreach (var d in drawables)
        {
            if (d.name.StartsWith("Mosaic"))
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
    public static void BeginNovelLive2DScale(NovelLive2DController __instance)
    {
        var root = __instance._canvasRoot;
        var originalScale = root.localScale;
        _novelLive2DOriginalScales[root] = originalScale;
        root.localScale = ScaleNovelLive2D(originalScale, GetNovelLive2DScale());
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelLive2DController), nameof(NovelLive2DController.Release))]
    public static void EndNovelLive2DScale(NovelLive2DController __instance)
    {
        var root = __instance._canvasRoot;
        if (_novelLive2DOriginalScales.Remove(root, out var originalScale))
            root.localScale = originalScale;
    }

    internal static void UpdateNovelLive2DScale()
    {
        if (_novelLive2DScaleSavePending && Time.unscaledTime >= _novelLive2DScaleSaveTime)
            SaveNovelLive2DScale();

        bool controlPressed =
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (_novelLive2DOriginalScales.Count == 0 || !controlPressed)
            return;

        float delta = Input.mouseScrollDelta.y;
        if (delta == 0)
            return;

        float scale = GetNovelLive2DScale();
        _novelLive2DScale = Mathf.Clamp(
            Mathf.Round((scale + delta * 0.01f) * 100f) / 100f,
            0.1f,
            10.0f
        );
        _novelLive2DScaleSaveTime = Time.unscaledTime + NovelLive2DScaleSaveDelay;
        _novelLive2DScaleSavePending = true;
        ApplyNovelLive2DScale();
    }

    internal static void ReloadNovelLive2DScale()
    {
        _novelLive2DScaleSavePending = false;
        float scale = Config.NovelLive2DScale.Value;
        if (_novelLive2DScale != scale)
        {
            _novelLive2DScale = scale;
            ApplyNovelLive2DScale();
        }
    }

    internal static void FlushNovelLive2DScale()
    {
        if (_novelLive2DScaleSavePending)
            SaveNovelLive2DScale();
    }

    private static float GetNovelLive2DScale()
    {
        if (float.IsNaN(_novelLive2DScale))
            _novelLive2DScale = Config.NovelLive2DScale.Value;

        return _novelLive2DScale;
    }

    private static void ApplyNovelLive2DScale()
    {
        foreach (var (root, originalScale) in _novelLive2DOriginalScales)
            root.localScale = ScaleNovelLive2D(originalScale, _novelLive2DScale);
    }

    private static Vector3 ScaleNovelLive2D(Vector3 originalScale, float scale) =>
        new(originalScale.x * scale, originalScale.y * scale, originalScale.z);

    private static void SaveNovelLive2DScale()
    {
        _novelLive2DScaleSavePending = false;
        Config.NovelLive2DScale.Value = _novelLive2DScale;
    }

    //[HarmonyPrefix]
    //[HarmonyPatch(
    //    typeof(NetherMapViewController),
    //    nameof(NetherMapViewController.UpdateMapViewVisibilityStates)
    //)]
    //public static void SetNetherFloorVisibility(
    //    NetherMapModel mapModel,
    //    ref NetherFloorModel currentFloorModel
    //)
    //{
    //    currentFloorModel = new NetherFloorModel
    //    {
    //        FloorLevel = int.MaxValue - NetherMapViewController.VisibleFloorLevelOffset,
    //    };
    //}
}
