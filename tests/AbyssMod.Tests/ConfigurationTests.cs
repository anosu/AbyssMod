using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AbyssMod.Patches;
using AbyssMod.Services;
using BepInEx.Configuration;
using UnityEngine;
using Utility.Notifications;
using Xunit;

namespace AbyssMod.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AbyssMod-" + Guid.NewGuid()
    );
    private readonly ConfigFile _file;
    private readonly RuntimeController _runtime = new();

    public ConfigurationTests()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "AbyssMod.cfg");
        File.WriteAllText(
            path,
            """
            [General]
            NovelLive2DScale = 1.5
            HideNovelGlasses = true
            [Translation]
            Enabled = false
            UIEnabled = false
            [Translation.Cache]
            Directory = custom/translations
            """
        );
        _file = new ConfigFile(path, false);
        Config.Initialize(_file);
        Invoke("OnEnable");
        TranslationPatch.RefreshCount = 0;
        Toast.Messages.Clear();
        Input.Pressed.Clear();
        Input.mouseScrollDelta = default;
        Time.unscaledTime = 0;
    }

    [Fact]
    public void ExistingConfigSurvivesBindingAndLiveChangesKeepStartupSnapshots()
    {
        Assert.True(Config.HideNovelGlasses.Value);
        Assert.Equal(1.5f, Config.NovelLive2DScale.Value);
        Assert.Equal("custom/translations", Config.TranslationCacheDirectory.Value);

        Config.Translation.Value = true;
        Config.UiTranslation.Value = true;
        Assert.False(Config.MasterDataTranslationEnabledAtStartup);
        Assert.False(Config.UiTranslationEnabledAtStartup);

        var saved = new ConfigFile(_file.ConfigFilePath, false);
        Assert.True(saved.Bind("Translation", "Enabled", false).Value);
        Assert.True(saved.Bind("General", "HideNovelGlasses", false).Value);
        Assert.Equal("custom/translations", saved.Bind("Translation.Cache", "Directory", "").Value);
    }

    [Fact]
    public async Task FileReloadDefersSceneChangesUntilTheNextFrame()
    {
        var root = new Transform { localScale = new Vector3(2, 3, 4) };
        NovelLive2DScaleController.Track(root);
        File.WriteAllText(
            _file.ConfigFilePath,
            """
            [General]
            NovelLive2DScale = 2
            [Translation]
            Enabled = true
            UIEnabled = true
            """
        );

        await Task.Run(_file.Reload);
        Assert.Equal(0, TranslationPatch.RefreshCount);
        Assert.Equal(3f, root.localScale.x);
        Invoke("Update");
        Assert.Equal(1, TranslationPatch.RefreshCount);
        Assert.Equal(4f, root.localScale.x);
        Assert.Equal(6f, root.localScale.y);
        Assert.Equal(4f, root.localScale.z);
        Assert.False(Config.MasterDataTranslationEnabledAtStartup);
        Assert.False(Config.UiTranslationEnabledAtStartup);
    }

    [Fact]
    public void HotkeysUpdateTheSamePersistedEntries()
    {
        bool effects = Config.NovelStageVolume.Value;
        bool voice = Config.VoiceInterruption.Value;
        Input.Pressed.UnionWith(new[] { KeyCode.F6, KeyCode.F8, KeyCode.F9 });
        Invoke("Update");
        Input.Pressed.Clear();

        Assert.Equal(!effects, Config.NovelStageVolume.Value);
        Assert.Equal(!voice, Config.VoiceInterruption.Value);
        Assert.True(Config.Translation.Value);
        Assert.Equal(1, TranslationPatch.RefreshCount);
        Invoke("Update");
        Assert.Equal(1, TranslationPatch.RefreshCount);
    }

    [Fact]
    public void ConfigChangeWinsOverPendingScrollAndReleaseRestoresOriginalScale()
    {
        var root = new Transform { localScale = new Vector3(2, 3, 4) };
        NovelLive2DScaleController.Track(root);
        Scroll(5);
        Assert.Equal(1.5f, Config.NovelLive2DScale.Value);

        Config.NovelLive2DScale.Value = 2;
        Invoke("Update");
        Time.unscaledTime = 3;
        Invoke("Update");
        Assert.Equal(2f, Config.NovelLive2DScale.Value);
        Assert.Equal(4f, root.localScale.x);

        NovelLive2DScaleController.Release(root);
        Assert.Equal(2f, root.localScale.x);
        Assert.Equal(3f, root.localScale.y);
        Assert.Equal(4f, root.localScale.z);
    }

    [Fact]
    public void ScrollAfterDelayedSaveIsNotOverwrittenByItsOwnConfigEvent()
    {
        var root = new Transform { localScale = new Vector3(1, 1, 1) };
        NovelLive2DScaleController.Track(root);
        Scroll(2);
        Time.unscaledTime = 2;
        Invoke("Update");
        Assert.Equal(1.52f, Config.NovelLive2DScale.Value);

        Scroll(1);
        Invoke("Update");
        Assert.Equal(1.53f, root.localScale.x, 2);
        Time.unscaledTime = 4;
        Invoke("Update");
        Assert.Equal(1.53f, Config.NovelLive2DScale.Value, 2);
    }

    [Fact]
    public void ShutdownAppliesPendingConfigBeforeFlushingScrollAndRestoresTheScene()
    {
        var root = new Transform { localScale = new Vector3(2, 3, 4) };
        NovelLive2DScaleController.Track(root);
        Scroll(5);
        Config.NovelLive2DScale.Value = 2;

        Invoke("OnDisable");
        NovelLive2DScaleController.Shutdown();
        Assert.Equal(2f, Config.NovelLive2DScale.Value);
        Assert.Equal(2f, root.localScale.x);
        Assert.Equal(3f, root.localScale.y);
        Assert.Equal(4f, root.localScale.z);
    }

    [Fact]
    public void ResourceStringsAreTrimmedBeforeSavingAndBuildingUrls()
    {
        Config.TranslationCDN.Value = "  https://example.com/translations/  ";
        Config.TranslationCacheDirectory.Value = "  custom path/translations  ";
        Config.FontBundlePath.Value = "  AbyssMod/fonts/custom font  ";

        Assert.Equal("https://example.com/translations/", Config.TranslationCDN.Value);
        Assert.Equal("custom path/translations", Config.TranslationCacheDirectory.Value);
        Assert.Equal("AbyssMod/fonts/custom font", Config.FontBundlePath.Value);
        Assert.Equal(
            "https://example.com/translations/manifest/zh_Hans.json",
            new Uri(
                TranslationPaths.BuildRemoteUrl(
                    Config.TranslationCDN.Value,
                    "manifest/zh_Hans.json"
                )
            ).AbsoluteUri
        );
        var saved = new ConfigFile(_file.ConfigFilePath, false);
        Assert.Equal(Config.TranslationCDN.Value, saved.Bind("Translation", "CDN", "").Value);
        Assert.Equal(
            Config.TranslationCacheDirectory.Value,
            saved.Bind("Translation.Cache", "Directory", "").Value
        );
        Assert.Equal(
            Config.FontBundlePath.Value,
            saved.Bind("Translation.Font", "AssetBundlePath", "").Value
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/translations")]
    [InlineData("https://")]
    public void InvalidCdnFallsBackToDefault(string value)
    {
        Config.TranslationCDN.Value = "http://localhost:33333/translations";
        Config.TranslationCDN.Value = value;
        Assert.Equal(Config.TranslationCDN.DefaultValue, Config.TranslationCDN.Value);
        var saved = new ConfigFile(_file.ConfigFilePath, false);
        Assert.Equal(
            Config.TranslationCDN.DefaultValue,
            saved.Bind("Translation", "CDN", "").Value
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyResourcePathsFallBackToDefault(string value)
    {
        Config.TranslationCacheDirectory.Value = value;
        Config.FontBundlePath.Value = value;
        Assert.Equal(
            Config.TranslationCacheDirectory.DefaultValue,
            Config.TranslationCacheDirectory.Value
        );
        Assert.Equal(Config.FontBundlePath.DefaultValue, Config.FontBundlePath.Value);
    }

    [Fact]
    public void ResourceValidationAlsoAppliesToStartupAndFileReload()
    {
        string invalidConfig = """
            [Translation]
            CDN = ftp://example.com/translations
            [Translation.Cache]
            Directory =
            [Translation.Font]
            AssetBundlePath =
            """;
        File.WriteAllText(_file.ConfigFilePath, invalidConfig);
        _file.Reload();
        Assert.Equal(Config.TranslationCDN.DefaultValue, Config.TranslationCDN.Value);
        Assert.Equal(
            Config.TranslationCacheDirectory.DefaultValue,
            Config.TranslationCacheDirectory.Value
        );
        Assert.Equal(Config.FontBundlePath.DefaultValue, Config.FontBundlePath.Value);

        Invoke("OnDisable");
        Config.Shutdown();
        File.WriteAllText(_file.ConfigFilePath, invalidConfig);
        Config.Initialize(new ConfigFile(_file.ConfigFilePath, false));
        Invoke("OnEnable");
        Assert.Equal(Config.TranslationCDN.DefaultValue, Config.TranslationCDN.Value);
        Assert.Equal(
            Config.TranslationCacheDirectory.DefaultValue,
            Config.TranslationCacheDirectory.Value
        );
        Assert.Equal(Config.FontBundlePath.DefaultValue, Config.FontBundlePath.Value);
    }

    [Fact]
    public void CommittedResourceTextAcceptsLocalHttpAndInternalSpaces()
    {
        const string url = "http://localhost:33333/translations";
        const string path = "custom path/translations";
        Config.TranslationCDN.Value = url;
        Config.TranslationCacheDirectory.Value = path;
        Assert.Equal(url, Config.TranslationCDN.Value);
        Assert.Equal(path, Config.TranslationCacheDirectory.Value);
        _file.Reload();
        Assert.Equal(url, Config.TranslationCDN.Value);
        Assert.Equal(path, Config.TranslationCacheDirectory.Value);
    }

    [Fact]
    public void ContinuousEditsDoNotQueueToastsButSwitchesStillNotify()
    {
        for (int i = 0; i < 50; i++)
            Config.NovelLive2DScale.Value = 1 + i * 0.01f;
        Config.TranslationCacheDirectory.Value = "custom/new/path";
        Assert.Empty(Toast.Messages);
        Assert.Contains(Logger.Messages, message => message.Contains("NovelLive2DScale =>"));

        Config.VoiceInterruption.Value = !Config.VoiceInterruption.Value;
        Assert.Single(Toast.Messages);
    }

    [Fact]
    public void BepInExClampsScaleAndLogsMaskTheCryptoKey()
    {
        Config.NovelLive2DScale.Value = 99;
        Assert.Equal(10f, Config.NovelLive2DScale.Value);
        Config.NovelLive2DScale.Value = -1;
        Assert.Equal(0.1f, Config.NovelLive2DScale.Value);
        Config.TranslationCryptoKey.Value = "test-private-value";
        Assert.Contains(Logger.Messages, message => message.Contains("Key => ***"));
        Assert.DoesNotContain(Logger.Messages, message => message.Contains("test-private-value"));

        Config.Shutdown();
        Toast.Messages.Clear();
        Config.VoiceInterruption.Value = !Config.VoiceInterruption.Value;
        Assert.Empty(Toast.Messages);
    }

    private void Scroll(float delta)
    {
        Input.Pressed.Add(KeyCode.LeftControl);
        Input.mouseScrollDelta = new Vector2(0, delta);
        Invoke("Update");
        Input.Pressed.Clear();
        Input.mouseScrollDelta = default;
    }

    private void Invoke(string callback) =>
        typeof(RuntimeController)
            .GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_runtime, null);

    public void Dispose()
    {
        Invoke("OnDisable");
        NovelLive2DScaleController.Shutdown();
        Config.Shutdown();
        Directory.Delete(_directory, recursive: true);
    }
}
