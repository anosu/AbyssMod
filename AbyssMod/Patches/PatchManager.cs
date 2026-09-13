using HarmonyLib;

namespace AbyssMod.Patches;

/// <summary>
/// Harmony 补丁管理器。负责初始化所有子补丁类、提供共享工具方法。
/// </summary>
public static class PatchManager
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
        _harmony.PatchAll(typeof(SettingsMenuInputPatch));
        // Do not patch UnityEngine.Input (or its UI bypass hooks) on this IL2CPP build.
        // GetKeyDownInt's generated original re-enters the detour and stack-overflows
        // on the very first Hotkey.Update, even while the settings menu is closed.
        // Only game-level handlers and raycast filtering are enabled here.
        _harmony.PatchAll(typeof(SettingsMenuRaycastPatch));
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
    }
}
