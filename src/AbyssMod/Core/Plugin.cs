using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using AbyssMod.Patches;
using AbyssMod.Services;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using UnityEngine;
using Utility.Assets;
using Utility.Notifications;

namespace AbyssMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private const int HttpTimeoutSeconds = 10;
    private const int PooledConnectionLifetimeMinutes = 5;
    private const int PooledConnectionIdleTimeoutMinutes = 2;

    internal static new ManualLogSource Log { get; private set; }
    internal static MonoBehaviour Instance { get; private set; }
    internal static TranslationManager Trans { get; private set; }
    internal static ImageReplacementManager Images { get; private set; }
    private static HttpClient _httpClient;
    private static HttpClient _translationHttpClient;

    public override void Load()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch { }

#if DEBUG
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--offline") || args.Contains("-o"))
            AbyssMod.Config.OfflineStartup = true;
#endif

        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Toast.Initialize();
        AbyssMod.Config.Initialize(base.Config);
        Instance = AddComponent<RuntimeController>();
        NovelStageVolumeController.Initialize();

        _httpClient = CreateHttpClient(decryptTranslations: false);
        _translationHttpClient = CreateHttpClient(decryptTranslations: true);
        InitializeTranslation();
        if (AbyssMod.Config.UiTranslationEnabledAtStartup)
            InitializeImageReplacements(_httpClient, _translationHttpClient);
        InitializeFont();
        if (AbyssMod.Config.Translation.Value || AbyssMod.Config.UiTranslationEnabledAtStartup)
            _ = Trans.EnsureStaticTranslationsLoadedAsync();
        PatchManager.Initialize();

        Toast.Success(
            MyPluginInfo.PLUGIN_NAME,
            $"Mod 加载成功，版本: {MyPluginInfo.PLUGIN_VERSION}"
        );
        _ = UpdateChecker.CheckAsync(_httpClient, MyPluginInfo.PLUGIN_VERSION);
    }

    private static HttpClient CreateHttpClient(bool decryptTranslations)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(PooledConnectionIdleTimeoutMinutes),
        };
        HttpMessageHandler pipeline = decryptTranslations
            ? new TranslationCryptoHandler(
                handler,
                () =>
                    (
                        AbyssMod.Config.TranslationCryptoTag.Value,
                        AbyssMod.Config.TranslationCryptoKey.Value
                    )
            )
            : handler;
        var client = new HttpClient(pipeline)
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{MyPluginInfo.PLUGIN_GUID}/{MyPluginInfo.PLUGIN_VERSION}"
        );

        return client;
    }

    private static void InitializeTranslation()
    {
        MigrateTranslationCache();
        string cacheDir = ResolvePluginPath(AbyssMod.Config.TranslationCacheDirectory.Value);
        var cache = new TranslationCache(
            AbyssMod.Config.TranslationCDN.Value,
            cacheDir,
            AbyssMod.Config.TranslationLanguage.Value,
            AbyssMod.Config.TranslationPreferLocalFiles.Value,
            _translationHttpClient
        );

        Trans = new TranslationManager(
            cache,
            AbyssMod.Config.MasterDataTranslationEnabledAtStartup,
            AbyssMod.Config.UiTranslationEnabledAtStartup
        );
    }

    private static void InitializeFont()
    {
        var font = new AssetBundleLoader<TMP_FontAsset>(
            ResolvePluginPath(AbyssMod.Config.FontBundlePath.Value)
        );
        Instance.StartCoroutine(
            font.Load(() =>
                {
                    Logger.Info($"Font loaded: {font.Asset.name}");
                    TMP_Settings.fallbackFontAssets.Add(font.Asset);
                })
                .WrapToIl2Cpp()
        );
    }

    private static void MigrateTranslationCache()
    {
        try
        {
            AbyssMod.Config.TranslationCacheDirectory.Value =
                TranslationPaths.MigrateLegacyCacheDirectory(
                    Paths.PluginPath,
                    AbyssMod.Config.TranslationCacheDirectory.Value
                );
        }
        catch (Exception e)
        {
            Logger.Warn($"Failed to migrate translation cache directory: {e.Message}");
        }
    }

    private static void InitializeImageReplacements(
        HttpClient imageClient,
        HttpClient manifestClient
    )
    {
        string replacementRoot = Path.Combine(
            Paths.PluginPath,
            MyPluginInfo.PLUGIN_GUID,
            "cache",
            "replacements"
        );
        var cache = new ImageReplacementCache(
            AbyssMod.Config.TranslationCDN.Value,
            AbyssMod.Config.TranslationLanguage.Value,
            replacementRoot,
            imageClient,
            manifestClient
        );
        cache.SyncAsync().GetAwaiter().GetResult();
        Images = new ImageReplacementManager(replacementRoot);
        Images.Initialize();
    }

    private static string ResolvePluginPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(Paths.PluginPath, path);

    public override bool Unload()
    {
        if (Instance != null)
        {
            Instance.enabled = false;
            UnityEngine.Object.Destroy(Instance);
            Instance = null;
        }
        NovelLive2DScaleController.Shutdown();
        NovelStageVolumeController.Shutdown();
        PatchManager.Shutdown();
        AbyssMod.Config.Shutdown();
        Images?.Dispose();
        Images = null;
        Trans = null;
        _httpClient?.Dispose();
        _httpClient = null;
        _translationHttpClient?.Dispose();
        _translationHttpClient = null;
        Toast.Clear();
        return base.Unload();
    }
}
