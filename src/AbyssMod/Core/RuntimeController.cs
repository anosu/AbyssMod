using System;
using System.Threading;
using AbyssMod.Patches;
using AbyssMod.Services;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace AbyssMod;

/// <summary>在 Unity 主线程应用配置变更、处理快捷键并维护剧情画面状态。</summary>
public sealed class RuntimeController : MonoBehaviour
{
    private int _refreshTranslation;
    private int _reloadScale;

    private void OnEnable()
    {
        Config.Translation.SettingChanged += OnTranslationChanged;
        Config.NovelLive2DScale.SettingChanged += OnScaleChanged;
    }

    private void OnDisable()
    {
        Config.Translation.SettingChanged -= OnTranslationChanged;
        Config.NovelLive2DScale.SettingChanged -= OnScaleChanged;
        if (Interlocked.Exchange(ref _reloadScale, 0) != 0)
            NovelLive2DScaleController.Reload();
        Interlocked.Exchange(ref _refreshTranslation, 0);
    }

    // ConfigFile.Reload 也可能由非 Unity 线程触发；回调只标记，下一帧读取最新值。
    [HideFromIl2Cpp]
    private void OnTranslationChanged(object sender, EventArgs e) =>
        Interlocked.Exchange(ref _refreshTranslation, 1);

    [HideFromIl2Cpp]
    private void OnScaleChanged(object sender, EventArgs e) =>
        Interlocked.Exchange(ref _reloadScale, 1);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
            Config.NovelStageVolume.Value = !Config.NovelStageVolume.Value;
        if (Input.GetKeyDown(KeyCode.F8))
            Config.Translation.Value = !Config.Translation.Value;
        if (Input.GetKeyDown(KeyCode.F9))
            Config.VoiceInterruption.Value = !Config.VoiceInterruption.Value;

        if (Interlocked.Exchange(ref _reloadScale, 0) != 0)
            NovelLive2DScaleController.Reload();
        if (Interlocked.Exchange(ref _refreshTranslation, 0) != 0)
            TranslationPatch.RefreshCurrentMessage();

        NovelLive2DScaleController.Update();
    }

    private void LateUpdate() => NovelStageVolumeController.Update();
}
