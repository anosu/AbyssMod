using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using Utility.Fonts;
using Utility.Toast;

namespace AbyssMod.Services;

/// <summary>
/// 翻译管理器：协调翻译数据的加载、缓存和查询。
/// 内部持有所有翻译数据。
/// </summary>
public class TranslationManager
{
    private readonly TranslationCache _cache;
    private readonly FontHelper _font;

    public Dictionary<string, string> Names { get; private set; } = [];
    public Dictionary<string, string> Titles { get; private set; } = [];
    public Dictionary<string, string> Descriptions { get; private set; } = [];
    public Dictionary<string, Dictionary<string, string>> Novels { get; private set; } = [];
    public FontHelper Font => _font;

    public TranslationManager(TranslationCache cache, FontHelper font)
    {
        _cache = cache;
        _font = font;
    }

    public void Initialize()
    {
        Plugin.Instance.StartCoroutine(
            _font
                .LoadAsync(() =>
                {
                    Logger.Info($"Font loaded: {_font.Asset.name}");
                    TMP_Settings.fallbackFontAssets.Add(_font.Asset);
                })
                .WrapToIl2Cpp()
        );
        _ = LoadTranslationAsync();
    }

    public async Task LoadTranslationAsync()
    {
        if (!Config.Translation.Value)
            return;

        await _cache.FetchManifestAsync();

        var nameTask = _cache.LoadAsync(TranslationPaths.Names);
        var titleTask = _cache.LoadAsync(TranslationPaths.Titles);
        var descTask = _cache.LoadAsync(TranslationPaths.Descriptions);
        await Task.WhenAll(nameTask, titleTask, descTask);

        if (nameTask.Result != null)
        {
            Names = nameTask.Result;
            Logger.Info($"Character names translation loaded. Total: {Names.Count}");
        }
        else
        {
            Logger.Warn("Character names translation load failed");
            Toast.Warn("加载失败", "角色名称翻译加载失败");
        }

        if (titleTask.Result != null)
        {
            Titles = titleTask.Result;
            Logger.Info($"Novel titles translation loaded. Total: {Titles.Count}");
        }
        else
        {
            Logger.Warn("Novel titles translation load failed");
            Toast.Warn("加载失败", "剧情标题翻译加载失败");
        }

        if (descTask.Result != null)
        {
            Descriptions = descTask.Result;
            Logger.Info($"Novel descriptions translation loaded. Total: {Descriptions.Count}");
        }
        else
        {
            Logger.Warn("Novel descriptions translation load failed");
            Toast.Warn("加载失败", "剧情概括翻译加载失败");
        }
    }

    public async Task GetNovelTranslationAsync(string novelId)
    {
        if (Novels.ContainsKey(novelId))
            return;

        var translations = await _cache.LoadAsync(TranslationPaths.Novels, novelId.ToString());
        if (translations != null)
        {
            Novels[novelId] = translations;
            Logger.Info($"Scenario translation loaded. Total: {translations.Count}");
        }
        else
        {
            Logger.Warn($"Translations loaded failed: {novelId}");
            Toast.Warn("加载失败", $"剧本ID: {novelId}");
        }
    }
}
