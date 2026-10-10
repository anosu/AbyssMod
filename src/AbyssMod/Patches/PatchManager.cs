using HarmonyLib;

namespace AbyssMod.Patches;

/// <summary>
/// 按启动配置注册 Harmony 补丁，并统一解除补丁与清理状态。
/// </summary>
internal static class PatchManager
{
    private static Harmony _harmony;

    /// <summary>
    /// 创建并注册所有 Harmony 补丁。
    /// </summary>
    public static void Initialize()
    {
        if (_harmony != null)
            return;

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(EnhancePatch));
        if (Config.UiTranslationEnabledAtStartup)
        {
            if (Plugin.Images?.Enabled == true)
                _harmony.PatchAll(typeof(ImageReplacementPatch));
            _harmony.PatchAll(typeof(UiTranslationPatch));
        }
        if (Config.MasterDataTranslationEnabledAtStartup)
            _harmony.PatchAll(typeof(MasterDataPatch));
        _harmony.PatchAll(typeof(TranslationPatch));
#if DEBUG
        _harmony.PatchAll(typeof(DebugPatch));
#endif
    }

    public static void Shutdown()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        MasterDataPatch.Reset();
        TranslationPatch.Reset();
    }
}
