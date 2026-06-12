using BepInEx.Configuration;
using Utility.Toast;

namespace AbyssMod
{
    /// <summary>
    /// 全局配置管理器。
    /// 负责初始化所有配置项并绑定事件监听。
    /// </summary>
    public static class Config
    {
#if DEBUG
        #region Debug
        public static ConfigEntry<bool> Offline;
        public static ConfigEntry<string> OfflineAPI;
        public static bool OfflineStartup;
        #endregion
#endif

        #region
        public static ConfigEntry<bool> DynamicMosaic;
        #endregion

        #region Translation
        public static ConfigEntry<bool> Translation;
        public static ConfigEntry<string> TranslationCDN;
        public static ConfigEntry<string> TranslationLanguage;
        #endregion

        #region Font
        public static ConfigEntry<string> FontBundlePath;
        #endregion

        /// <summary>
        /// 初始化配置系统。
        /// </summary>
        public static void Initialize()
        {
            BindAllEntries();
            BindSettingChangedLog();
        }

        private static void BindAllEntries()
        {
#if DEBUG
            #region Debug
            Offline = Plugin.ConfigFile.Bind(
                "Debug.Offline",
                "Enabled",
                false,
                "API localization for debug"
            );
            OfflineAPI = Plugin.ConfigFile.Bind(
                "Debug.Offline",
                "CDN",
                "http://localhost:33333/abyss/",
                "CDN for debug"
            );
            #endregion
#endif

            #region General
            DynamicMosaic = Plugin.ConfigFile.Bind(
                "General",
                "DynamicMosaic",
                false,
                "是否启用游戏内动态马赛克"
            );
            #endregion

            #region Translation
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
            #endregion

            #region Font
            FontBundlePath = Plugin.ConfigFile.Bind(
                "Translation.Font",
                "AssetBundlePath",
                $"{MyPluginInfo.PLUGIN_GUID}/fonts/ttcuyuanj",
                "TMP字体AssetBundle的路径，默认相对于插件目录，也可使用绝对路径"
            );
            #endregion
        }

        /// <summary>
        /// 绑定配置变更日志输出。
        /// </summary>
        private static void BindSettingChangedLog()
        {
            Plugin.ConfigFile.SettingChanged += (_, e) =>
            {
                var c = e.ChangedSetting;
                Plugin.Log.LogInfo(
                    $"[{c.Definition.Section}] {c.Definition.Key} => {c.BoxedValue}"
                );
                Toast.Info($"[{c.Definition.Section}]", $"{c.Definition.Key} => {c.BoxedValue}");
            };
        }
    }
}
