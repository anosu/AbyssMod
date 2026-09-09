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
        if (Config.TranslationEnabledAtStartup)
        {
            _harmony.PatchAll(typeof(MasterDataPatch));
            _harmony.PatchAll(typeof(UiTranslationPatch));
        }
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
