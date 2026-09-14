using AbyssMod.Patches;
using AbyssMod.Services;
using AbyssMod.UI;
using UnityEngine;

namespace AbyssMod;

/// <summary>
/// F10 打开设置菜单；可选兼容快捷键 F6/F8/F9 仅在菜单关闭时启用。
/// </summary>
public class Hotkey : MonoBehaviour
{
    private void Update()
    {
        MenuMouseIsolation.Update();
        MenuGameInputGuard.Update();
        if (Input.GetKeyDown(KeyCode.F10))
        {
            SettingsMenuController.Toggle();
            return;
        }
        if (MenuMouseIsolation.BlocksGame)
        {
            if (MenuMouseIsolation.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                SettingsMenuController.RequestClose();
            return;
        }

        EnhancePatch.UpdateNovelLive2DScale();
        if (!Config.LegacyHotkeys.Value)
            return;

        if (Input.GetKeyDown(KeyCode.F6))
            NovelStageVolumeController.Toggle();

        if (Input.GetKeyDown(KeyCode.F8))
        {
            Config.Translation.Value = !Config.Translation.Value;
            TranslationPatch.RefreshCurrentMessage();
        }

        if (Input.GetKeyDown(KeyCode.F9))
            Config.VoiceInterruption.Value = !Config.VoiceInterruption.Value;
    }

    private void LateUpdate()
    {
        NovelStageVolumeController.Update();
        SettingsMenuController.MaintainCursor();
    }
}
