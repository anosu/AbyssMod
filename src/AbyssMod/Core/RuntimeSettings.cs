using AbyssMod.Patches;
using AbyssMod.Services;

namespace AbyssMod;

internal static class RuntimeSettings
{
    internal static void ApplyChanges(
        SettingsValues previous,
        SettingsValues current,
        bool force = false
    )
    {
        if (force || previous.Live2DScale != current.Live2DScale)
            EnhancePatch.ReloadNovelLive2DScale();
        if (force || previous.Live2DEffects != current.Live2DEffects)
            NovelStageVolumeController.Reload();
        if (force || previous.Translation != current.Translation)
            TranslationPatch.RefreshCurrentMessage();
    }

    internal static void ReloadFromFile()
    {
        var previous = SettingsValues.Read();
        Plugin.ConfigFile.Reload();
        ApplyChanges(previous, SettingsValues.Read(), force: true);
        Logger.Info(
            "Config reloaded from settings menu; MasterData/UI translation and translation resources still follow startup settings."
        );
    }
}
