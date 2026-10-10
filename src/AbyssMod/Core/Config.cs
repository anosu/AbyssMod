using AbyssMod.Services;
using BepInEx.Configuration;
using Utility.Notifications;

namespace AbyssMod;

/// <summary>统一定义配置、显示元数据、启动快照和变更通知。</summary>
internal static class Config
{
    private const string DefaultTranslationCdn =
        "https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations";
    private const string DefaultFontBundlePath = MyPluginInfo.PLUGIN_GUID + "/fonts/ttcuyuanj";
    private static ConfigFile _config;

#if DEBUG
    public static ConfigEntry<bool> Offline { get; private set; }
    public static ConfigEntry<string> OfflineAPI { get; private set; }
    public static ConfigEntry<string> DmmSdkAPI { get; private set; }
    public static bool OfflineStartup;
#endif

    public static ConfigEntry<bool> DynamicMosaic { get; private set; }
    public static ConfigEntry<bool> SoundCaution { get; private set; }
    public static ConfigEntry<bool> VoiceInterruption { get; private set; }
    public static ConfigEntry<bool> HideNovelGlasses { get; private set; }
    public static ConfigEntry<bool> TitleMovie { get; private set; }
    public static ConfigEntry<float> NovelLive2DScale { get; private set; }
    public static ConfigEntry<bool> NovelStageVolume { get; private set; }

    public static ConfigEntry<bool> Translation { get; private set; }
    public static ConfigEntry<bool> UiTranslation { get; private set; }
    public static ConfigEntry<string> TranslationCDN { get; private set; }
    public static ConfigEntry<string> TranslationLanguage { get; private set; }
    public static ConfigEntry<string> TranslationCacheDirectory { get; private set; }
    public static ConfigEntry<bool> TranslationPreferLocalFiles { get; private set; }
    public static ConfigEntry<string> TranslationCryptoTag { get; private set; }
    public static ConfigEntry<string> TranslationCryptoKey { get; private set; }
    public static ConfigEntry<string> FontBundlePath { get; private set; }
    internal static bool MasterDataTranslationEnabledAtStartup { get; private set; }
    internal static bool UiTranslationEnabledAtStartup { get; private set; }

    public static void Initialize(ConfigFile config)
    {
        _config = config;
        BindGeneralEntries(config);
        BindTranslationEntries(config);
#if DEBUG
        BindDebugEntries(config);
#endif
        MasterDataTranslationEnabledAtStartup = Translation.Value;
        UiTranslationEnabledAtStartup = UiTranslation.Value;
        config.SettingChanged += OnSettingChanged;
    }

    public static void Shutdown()
    {
        if (_config != null)
            _config.SettingChanged -= OnSettingChanged;
        _config = null;
    }

    private static void BindGeneralEntries(ConfigFile config)
    {
        DynamicMosaic = config.Bind(
            "General",
            "DynamicMosaic",
            false,
            new ConfigDescription(
                "是否启用游戏内动态马赛克；下次载入 Live2D 场景时生效。",
                null,
                new ConfigurationManagerAttributes { DispName = "动态马赛克", Order = 70 }
            )
        );
        SoundCaution = config.Bind(
            "General",
            "SoundCaution",
            false,
            new ConfigDescription(
                "是否启用进入游戏时的音量提醒弹窗；下次显示该弹窗时生效。",
                null,
                new ConfigurationManagerAttributes { DispName = "音量提醒", Order = 60 }
            )
        );
        VoiceInterruption = config.Bind(
            "General",
            "VoiceInterruption",
            false,
            new ConfigDescription(
                "剧情中播放下一段无声文本时是否中断当前角色语音；即时生效。快捷键：F9。",
                null,
                new ConfigurationManagerAttributes { DispName = "语音中断", Order = 50 }
            )
        );
        HideNovelGlasses = config.Bind(
            "General",
            "HideNovelGlasses",
            false,
            new ConfigDescription(
                "是否隐藏寝室 CG 中角色的眼镜；下次载入 Live2D 场景时生效。",
                null,
                new ConfigurationManagerAttributes { DispName = "隐藏眼镜", Order = 40 }
            )
        );
        TitleMovie = config.Bind(
            "General",
            "TitleMovie",
            true,
            new ConfigDescription(
                "是否播放进入游戏时的标题动画；下次进入标题画面时生效。",
                null,
                new ConfigurationManagerAttributes { DispName = "标题动画", Order = 30 }
            )
        );
        NovelLive2DScale = config.Bind(
            "General",
            "NovelLive2DScale",
            1.0f,
            new ConfigDescription(
                "H 场景尺寸的缩放倍率，1 为原始大小；即时生效。也可按住 Ctrl 滚动鼠标滚轮调整。",
                new AcceptableValueRange<float>(0.1f, 10.0f),
                new ConfigurationManagerAttributes
                {
                    DispName = "H 场景缩放",
                    Order = 20,
                    ShowRangeAsPercent = false,
                }
            )
        );
        NovelStageVolume = config.Bind(
            "General",
            "NovelStageVolume",
            true,
            new ConfigDescription(
                "是否启用 H 场景附加泛光、色差滤镜，保留舞台基础效果；即时生效。快捷键：F6。",
                null,
                new ConfigurationManagerAttributes { DispName = "H 场景滤镜", Order = 10 }
            )
        );
    }

    private static void BindTranslationEntries(ConfigFile config)
    {
        Translation = config.Bind(
            "Translation",
            "Enabled",
            true,
            new ConfigDescription(
                "是否开启剧情和 MasterData 翻译；剧情即时切换，MasterData 修改后需重启游戏。快捷键：F8（仅切换剧情）。",
                null,
                new ConfigurationManagerAttributes { DispName = "剧情与资料翻译", Order = 40 }
            )
        );
        UiTranslation = config.Bind(
            "Translation",
            "UIEnabled",
            true,
            new ConfigDescription(
                "是否开启界面文字和图片替换翻译；修改后需重启游戏，不受 F8 影响。",
                null,
                new ConfigurationManagerAttributes { DispName = "界面与图片翻译", Order = 30 }
            )
        );
        TranslationLanguage = config.Bind(
            "Translation",
            "Language",
            "zh_Hans",
            new ConfigDescription(
                "翻译语言，目前支持 zh_Hans（简体中文）；修改后需重启游戏。",
                new AcceptableValueList<string>("zh_Hans"),
                new ConfigurationManagerAttributes { DispName = "翻译语言", Order = 20 }
            )
        );
        TranslationCDN = config.Bind(
            "Translation",
            "CDN",
            DefaultTranslationCdn,
            new ConfigDescription(
                "翻译资源根地址，须为完整的 HTTP 或 HTTPS URL；提交时去除首尾空格，无效地址使用默认值；修改后需重启游戏。",
                AcceptableResourceString.HttpUrl(DefaultTranslationCdn),
                new ConfigurationManagerAttributes
                {
                    DispName = "翻译 CDN",
                    Order = 10,
                    IsAdvanced = true,
                }
            )
        );
        TranslationCacheDirectory = config.Bind(
            "Translation.Cache",
            "Directory",
            TranslationPaths.DefaultCacheDirectory,
            new ConfigDescription(
                "翻译缓存目录；相对路径以 BepInEx/plugins 为基准，也可使用绝对路径；提交时去除首尾空格，留空使用默认值；修改后需重启游戏。",
                AcceptableResourceString.Path(TranslationPaths.DefaultCacheDirectory),
                new ConfigurationManagerAttributes
                {
                    DispName = "缓存目录",
                    Order = 20,
                    IsAdvanced = true,
                }
            )
        );
        TranslationPreferLocalFiles = config.Bind(
            "Translation.Cache",
            "PreferLocalFiles",
            false,
            new ConfigDescription(
                "本地翻译文件存在时是否忽略清单哈希并优先使用本地文件（manifest 除外）；修改后需重启游戏。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "优先使用本地文件",
                    Order = 10,
                    IsAdvanced = true,
                }
            )
        );
        FontBundlePath = config.Bind(
            "Translation.Font",
            "AssetBundlePath",
            DefaultFontBundlePath,
            new ConfigDescription(
                "TMP 字体 AssetBundle 路径；相对路径以 BepInEx/plugins 为基准，也可使用绝对路径；提交时去除首尾空格，留空使用默认值；修改后需重启游戏。",
                AcceptableResourceString.Path(DefaultFontBundlePath),
                new ConfigurationManagerAttributes
                {
                    DispName = "字体资源路径",
                    Order = 10,
                    IsAdvanced = true,
                }
            )
        );
        TranslationCryptoTag = config.Bind(
            "Translation.Crypto",
            "Tag",
            "ENC:",
            new ConfigDescription(
                "翻译文本加密标签；留空则不解密。仅对之后下载的资源生效，已加载内容不会重载。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "加密标签",
                    Order = 20,
                    IsAdvanced = true,
                }
            )
        );
        TranslationCryptoKey = config.Bind(
            "Translation.Crypto",
            "Key",
            "woshitonghuadawang",
            new ConfigDescription(
                "翻译文本解密密钥；使用加密标签时不能为空。仅对之后下载的资源生效，已加载内容不会重载。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "解密密钥",
                    Order = 10,
                    IsAdvanced = true,
                }
            )
        );
    }

#if DEBUG
    private static void BindDebugEntries(ConfigFile config)
    {
        Offline = config.Bind(
            "Debug.Offline",
            "Enabled",
            false,
            new ConfigDescription(
                "是否将游戏 API 请求转向本地调试服务；修改后需重启游戏。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "离线调试",
                    Order = 30,
                    IsAdvanced = true,
                }
            )
        );
        OfflineAPI = config.Bind(
            "Debug.Offline",
            "API",
            "http://localhost:33333/abyss/",
            new ConfigDescription(
                "本地游戏 API 地址；修改后需重启游戏。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "游戏 API",
                    Order = 20,
                    IsAdvanced = true,
                }
            )
        );
        DmmSdkAPI = config.Bind(
            "Debug.Offline",
            "DmmSdkAPI",
            "http://localhost:33333/dmmsdk",
            new ConfigDescription(
                "本地 DMM SDK API 地址；修改后需重启游戏。",
                null,
                new ConfigurationManagerAttributes
                {
                    DispName = "DMM SDK API",
                    Order = 10,
                    IsAdvanced = true,
                }
            )
        );
    }
#endif

    private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        var setting = e.ChangedSetting;
        object value = ReferenceEquals(setting, TranslationCryptoKey) ? "***" : setting.BoxedValue;
        Logger.Info($"[{setting.Definition.Section}] {setting.Definition.Key} => {value}");
        // 仅开关显示提示；数值和文本编辑保留日志，避免连续调整挤占通知队列。
        if (setting.SettingType == typeof(bool))
            Toast.Info($"[{setting.Definition.Section}]", $"{setting.Definition.Key} => {value}");
    }
}
