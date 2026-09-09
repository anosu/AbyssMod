using AbyssMod.Patches;
using UnityEngine;

namespace AbyssMod;

/// <summary>
/// 快捷键处理（MonoBehaviour）。F8 切换翻译、F9 切换语音中断、F10 重载配置。
/// </summary>
public class Hotkey : MonoBehaviour
{
    private void Update()
    {
        EnhancePatch.UpdateNovelLive2DScale();

        if (Input.GetKeyDown(KeyCode.F8))
        {
            Config.Translation.Value = !Config.Translation.Value;
            TranslationPatch.RefreshCurrentMessage();
        }

        if (Input.GetKeyDown(KeyCode.F9))
            Config.VoiceInterruption.Value = !Config.VoiceInterruption.Value;

        if (Input.GetKeyDown(KeyCode.F10))
        {
            Plugin.ConfigFile.Reload();
            EnhancePatch.ReloadNovelLive2DScale();
            TranslationPatch.RefreshCurrentMessage();
            Logger.Info(
                "Config reloaded; translation source, cache, and font changes require restart"
            );
        }
    }
}
