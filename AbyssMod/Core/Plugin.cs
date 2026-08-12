using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using AbyssMod.Patches;
using AbyssMod.Services;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Utility.Fonts;
using Utility.Toast;

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

        AddComponent<ToastUI>();
        AbyssMod.Config.Initialize();
        Instance = AddComponent<Hotkey>();

        HttpClient httpClient = Initialize();
        InitializeImageReplacements(httpClient);
        PatchManager.Initialize();
        MasterMapping.Load();
        Trans.Initialize();

        Toast.Success(
            MyPluginInfo.PLUGIN_NAME,
            $"Mod 加载成功，版本: {MyPluginInfo.PLUGIN_VERSION}"
        );
        _ = UpdateChecker.CheckAsync(httpClient, MyPluginInfo.PLUGIN_VERSION);
    }

    private static HttpClient Initialize()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(PooledConnectionIdleTimeoutMinutes),
        };
        var httpClient = new HttpClient(new CryptoHandler(handler))
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds),
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{MyPluginInfo.PLUGIN_GUID}/{MyPluginInfo.PLUGIN_VERSION}"
        );

        var cache = new TranslationCache(
            AbyssMod.Config.TranslationCDN.Value,
            Path.Combine(Paths.PluginPath, MyPluginInfo.PLUGIN_GUID, "cache"),
            AbyssMod.Config.TranslationLanguage.Value,
            httpClient
        );

        string fontPath = AbyssMod.Config.FontBundlePath.Value;
        string resolvedPath = Path.IsPathRooted(fontPath)
            ? fontPath
            : Path.Combine(Paths.PluginPath, fontPath);

        Trans = new TranslationManager(cache, new FontHelper(resolvedPath));
        return httpClient;
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

    public override bool Unload()
    {
        EnhancePatch.FlushNovelLive2DScale();
        Images?.Dispose();
        Images = null;
        Toast.Clear();
        return base.Unload();
    }
}
