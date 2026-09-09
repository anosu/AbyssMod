using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using AbyssMod.Patches;
using AbyssMod.Services;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
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

    public static new ManualLogSource Log;
    public static ConfigFile ConfigFile;
    public static MonoBehaviour Instance;
    public static TranslationManager Trans;
    public static ImageReplacementManager Images;
    private static HttpClient _httpClient;

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
        ConfigFile = base.Config;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Toast.Initialize();
        AbyssMod.Config.Initialize();
        Instance = AddComponent<Hotkey>();

        Initialize();
        InitializeImageReplacements(_httpClient);
        Trans.Initialize();
        PatchManager.Initialize();

        Toast.Success(
            MyPluginInfo.PLUGIN_NAME,
            $"Mod 加载成功，版本: {MyPluginInfo.PLUGIN_VERSION}"
        );
        _ = UpdateChecker.CheckAsync(_httpClient, MyPluginInfo.PLUGIN_VERSION);
    }

    private static void Initialize()
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
        _httpClient = new HttpClient(new CryptoHandler(handler))
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{MyPluginInfo.PLUGIN_GUID}/{MyPluginInfo.PLUGIN_VERSION}"
        );

        string cacheDir = ResolvePluginPath(AbyssMod.Config.TranslationCacheDirectory.Value);
        var cache = new TranslationCache(
            AbyssMod.Config.TranslationCDN.Value,
            cacheDir,
            AbyssMod.Config.TranslationLanguage.Value,
            AbyssMod.Config.TranslationPreferLocalFiles.Value,
            _httpClient
        );

        Trans = new TranslationManager(
            cache,
            new AssetBundleLoader<TMP_FontAsset>(ResolvePluginPath(AbyssMod.Config.FontBundlePath.Value))
        );
    }

    private static void InitializeImageReplacements(HttpClient httpClient)
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
            httpClient
        );
        cache.SyncAsync().GetAwaiter().GetResult();
        Images = new ImageReplacementManager(replacementRoot);
        Images.Initialize();
    }

    private static string ResolvePluginPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(Paths.PluginPath, path);

    public override bool Unload()
    {
        EnhancePatch.FlushNovelLive2DScale();
        PatchManager.Shutdown();
        Images?.Dispose();
        Images = null;
        _httpClient?.Dispose();
        Toast.Clear();
        return base.Unload();
    }
}
