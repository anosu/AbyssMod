using System;
using AbyssMod.Services;
using BepInEx;
using BepInEx.Configuration;
using Utility.Notifications;

namespace AbyssMod;

/// <summary>
/// 全局配置：初始化所有 BepInEx 配置项并绑定变更事件。
/// </summary>
public static class Config
{
#if DEBUG
    public static ConfigEntry<bool> Offline;
    public static ConfigEntry<string> OfflineAPI;
    public static ConfigEntry<string> DmmSdkAPI;
    public static bool OfflineStartup;
#endif

    public static ConfigEntry<bool> DynamicMosaic;
    public static ConfigEntry<bool> SoundCaution;
    public static ConfigEntry<bool> VoiceInterruption;
    public static ConfigEntry<bool> TitleMovie;
    public static ConfigEntry<float> NovelLive2DScale;
    public static ConfigEntry<bool> NovelStageVolume;
    public static ConfigEntry<bool> LegacyHotkeys;
    internal static bool SuppressSettingToasts;

    public static ConfigEntry<bool> Translation;
    public static ConfigEntry<bool> UiTranslation;
    public static ConfigEntry<string> TranslationCDN;
    public static ConfigEntry<string> TranslationLanguage;
    public static ConfigEntry<string> TranslationCacheDirectory;
    public static ConfigEntry<bool> TranslationPreferLocalFiles;
    public static ConfigEntry<string> TranslationCryptoTag;
    public static ConfigEntry<string> TranslationCryptoKey;
    public static ConfigEntry<string> FontBundlePath;
    internal static bool MasterDataTranslationEnabledAtStartup { get; private set; }
    internal static bool UiTranslationEnabledAtStartup { get; private set; }

    public static void Initialize()
    {
        BindAllEntries();
        try
        {
            TranslationCacheDirectory.Value = TranslationPaths.MigrateLegacyCacheDirectory(
                Paths.PluginPath,
                TranslationCacheDirectory.Value
            );
        }
        catch (Exception e)
        {
            Logger.Warn($"Failed to migrate translation cache directory: {e.Message}");
        }
        MasterDataTranslationEnabledAtStartup = Translation.Value;
        UiTranslationEnabledAtStartup = UiTranslation.Value;
        Plugin.ConfigFile.SettingChanged += (_, e) =>
        {
            var c = e.ChangedSetting;
            object value = ReferenceEquals(c, TranslationCryptoKey) ? "***" : c.BoxedValue;
            Logger.Info($"[{c.Definition.Section}] {c.Definition.Key} => {value}");
            if (!SuppressSettingToasts)
                Toast.Info($"[{c.Definition.Section}]", $"{c.Definition.Key} => {value}");
        };
    }

    private static void BindAllEntries()
    {
#if DEBUG
        Offline = Plugin.ConfigFile.Bind("Debug.Offline", "Enabled", false, "API localization");
        OfflineAPI = Plugin.ConfigFile.Bind(
            "Debug.Offline",
            "API",
            "http://localhost:33333/abyss/",
            "API for debugging"
        );
        DmmSdkAPI = Plugin.ConfigFile.Bind(
            "Debug.Offline",
            "DmmSdkAPI",
            "http://localhost:33333/dmmsdk",
            "API for debugging"
        );
#endif

        DynamicMosaic = Plugin.ConfigFile.Bind(
            "General",
            "DynamicMosaic",
            false,
            "是否启用游戏内动态马赛克"
        );
        SoundCaution = Plugin.ConfigFile.Bind(
            "General",
            "SoundCaution",
            false,
            "是否启用进入游戏时的音量提醒弹窗"
        );
        VoiceInterruption = Plugin.ConfigFile.Bind(
            "General",
            "VoiceInterruption",
            false,
            "剧情中播放下一段无声文本时是否中断当前角色语音"
        );
        TitleMovie = Plugin.ConfigFile.Bind(
            "General",
            "TitleMovie",
            true,
            "是否开启进入游戏时的标题动画"
        );
        NovelLive2DScale = Plugin.ConfigFile.Bind(
            "General",
            "NovelLive2DScale",
            1.0f,
            new ConfigDescription(
                "H场景尺寸大小的缩放倍率；可在 MOD 设置菜单中调整，或在菜单关闭时按住 Ctrl 滚动鼠标滚轮",
                new AcceptableValueRange<float>(0.1f, 10.0f)
            )
        );
        NovelStageVolume = Plugin.ConfigFile.Bind(
            "General",
            "NovelStageVolume",
            true,
            "是否启用H场景滤镜（附加泛光、色差）；保留舞台基础效果；在 MOD 设置菜单中保存后即时生效，F6 仅在兼容快捷键开启时可用"
        );
        LegacyHotkeys = Plugin.ConfigFile.Bind(
            "Menu",
            "LegacyHotkeys",
            false,
            "启用兼容快捷键：F6 切换H场景滤镜、F8 切换剧情翻译、F9 切换语音中断；F10 始终打开菜单"
        );

        Translation = Plugin.ConfigFile.Bind(
            "Translation",
            "Enabled",
            true,
            "是否开启剧情和 MasterData 翻译；MasterData 仅在启动时读取，修改后重启生效；剧情可在菜单即时切换，F8 仅在兼容快捷键开启时可用"
        );
        UiTranslation = Plugin.ConfigFile.Bind(
            "Translation",
            "UIEnabled",
            true,
            "是否开启 UI 文本和图片替换翻译；仅在启动时读取，修改后重启生效，不受 F8 影响"
        );
        TranslationCDN = Plugin.ConfigFile.Bind(
            "Translation",
            "CDN",
            "https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations",
            "翻译加载的CDN，修改后重启生效"
        );
        TranslationLanguage = Plugin.ConfigFile.Bind(
            "Translation",
            "Language",
            "zh_Hans",
            "翻译语言，取值范围：[zh_Hans]，修改后重启生效"
        );
        TranslationCacheDirectory = Plugin.ConfigFile.Bind(
            "Translation.Cache",
            "Directory",
            TranslationPaths.DefaultCacheDirectory,
            "翻译缓存目录，默认位于 AbyssMod/cache/translations；相对路径以 BepInEx/plugins 为基准，也可使用绝对路径；修改后重启生效"
        );
        TranslationPreferLocalFiles = Plugin.ConfigFile.Bind(
            "Translation.Cache",
            "PreferLocalFiles",
            false,
            "本地翻译文件存在时是否忽略清单哈希并优先使用本地文件（manifest 除外）；修改后重启生效"
        );
        TranslationCryptoTag = Plugin.ConfigFile.Bind(
            "Translation.Crypto",
            "Tag",
            "ENC:",
            "翻译文本加密标签（可选）"
        );
        TranslationCryptoKey = Plugin.ConfigFile.Bind(
            "Translation.Crypto",
            "Key",
            "woshitonghuadawang",
            "翻译文本解密密钥（可选）"
        );
        FontBundlePath = Plugin.ConfigFile.Bind(
            "Translation.Font",
            "AssetBundlePath",
            $"{MyPluginInfo.PLUGIN_GUID}/fonts/ttcuyuanj",
            "TMP字体AssetBundle的路径，默认相对于插件目录，也可使用绝对路径；修改后重启生效"
        );
    }
}
