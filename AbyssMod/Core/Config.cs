using BepInEx.Configuration;
using Utility.Toast;

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

    public static ConfigEntry<bool> Translation;
    public static ConfigEntry<string> TranslationCDN;
    public static ConfigEntry<string> TranslationLanguage;
    public static ConfigEntry<string> TranslationCryptoTag;
    public static ConfigEntry<string> TranslationCryptoKey;
    public static ConfigEntry<string> FontBundlePath;

    public static void Initialize()
    {
        BindAllEntries();
        Plugin.ConfigFile.SettingChanged += (_, e) =>
        {
            var c = e.ChangedSetting;
            Logger.Info($"[{c.Definition.Section}] {c.Definition.Key} => {c.BoxedValue}");
            Toast.Info($"[{c.Definition.Section}]", $"{c.Definition.Key} => {c.BoxedValue}");
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

        Translation = Plugin.ConfigFile.Bind(
            "Translation",
            "Enabled",
            true,
            "是否开启游戏内剧情翻译"
        );
        TranslationCDN = Plugin.ConfigFile.Bind(
            "Translation",
            "CDN",
            "https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations",
            "翻译加载的CDN"
        );
        TranslationLanguage = Plugin.ConfigFile.Bind(
            "Translation",
            "Language",
            "zh_Hans",
            "翻译语言，取值范围：[zh_Hans]"
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
            "TMP字体AssetBundle的路径，默认相对于插件目录，也可使用绝对路径"
        );
    }
}
