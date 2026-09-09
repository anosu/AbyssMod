using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using Utility.Assets;
using Utility.Notifications;

namespace AbyssMod.Services;

/// <summary>
/// 翻译协调层：持有所有运行时翻译数据，提供统一查询入口。
/// </summary>
public class TranslationManager
{
    private readonly TranslationCache _cache;
    private readonly AssetBundleLoader<TMP_FontAsset> _font;
    private readonly object _loadLock = new();
    private Task _loadTask;
    private volatile bool _staticTranslationsLoaded;
    private volatile MasterDataTranslator _masterDataTranslator;

    private readonly ConcurrentDictionary<string, Task> _loadingNovels = new();

    private volatile Dictionary<string, string> _names;
    private volatile UiTextIndex _uiTexts;

    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _novels = new();

    public AssetBundleLoader<TMP_FontAsset> Font => _font;

    public TranslationManager(TranslationCache cache, AssetBundleLoader<TMP_FontAsset> font)
    {
        _cache = cache;
        _font = font;
    }

    public void Initialize()
    {
        Plugin.Instance.StartCoroutine(
            _font
                .Load(() =>
                {
                    Logger.Info($"Font loaded: {_font.Asset.name}");
                    TMP_Settings.fallbackFontAssets.Add(_font.Asset);
                })
                .WrapToIl2Cpp()
        );
        _ = EnsureStaticTranslationsLoadedAsync();
    }

    // ── 静态翻译加载 ────────────────────────

    public Task EnsureStaticTranslationsLoadedAsync()
    {
        if (!Config.TranslationEnabledAtStartup && !Config.Translation.Value)
            return Task.CompletedTask;

        lock (_loadLock)
        {
            if (
                _loadTask == null
                || _loadTask.IsCanceled
                || _loadTask.IsFaulted
                || (_loadTask.IsCompleted && !_staticTranslationsLoaded)
            )
                _loadTask = LoadTranslationAsync();

            return _loadTask;
        }
    }

    private async Task LoadTranslationAsync()
    {
        await _cache.FetchManifestAsync();

        var bundleTask = _masterDataTranslator == null ? _cache.LoadStaticBundleAsync() : null;
        var namesTask = _names == null ? _cache.LoadAsync(TranslationPaths.Names) : null;
        var uiTextsTask = _uiTexts == null ? _cache.LoadUiTextsAsync() : null;

        if (bundleTask != null)
        {
            var bundle = await bundleTask;
            if (bundle != null)
            {
                var masterTables =
                    new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
                foreach (var (type, table) in bundle)
                {
                    if (!IsMasterDataStaticType(type) || table == null)
                        continue;

                    masterTables[type] = table;
                }
                _masterDataTranslator = MasterDataTranslator.Create(masterTables);
                Logger.Info($"Static translation bundle loaded. Tables: {masterTables.Count}");
            }
            else
            {
                Logger.Warn("MasterData static translation bundle load failed.");
                Toast.Warning("加载失败", "MasterData 静态翻译合并包加载失败");
            }
        }

        if (namesTask != null)
        {
            var names = await namesTask;
            if (names != null)
            {
                _names = names;
                Logger.Info($"Static translation loaded [names]. Total: {names.Count}");
            }
            else
                Logger.Warn("Static translation load failed [names]");
        }

        if (uiTextsTask != null)
        {
            var uiTexts = await uiTextsTask;
            if (uiTexts != null)
            {
                _uiTexts = new UiTextIndex(uiTexts);
                Logger.Info($"Static translation loaded [ui_texts]. Paths: {uiTexts.Count}");
            }
            else
                Logger.Warn("Static translation load failed [ui_texts]");
        }

        _staticTranslationsLoaded =
            _masterDataTranslator != null && _names != null && _uiTexts != null;
    }

    // ── 查询 API ────────────────────────────

    internal bool HasUiTranslations => _uiTexts != null;

    public string TranslateName(string sourceText) =>
        !string.IsNullOrEmpty(sourceText)
        && _names != null
        && _names.TryGetValue(sourceText, out var translated)
        && !string.IsNullOrEmpty(translated)
            ? translated
            : sourceText;

    public string TranslateUiText(string path, string sourceText)
    {
        var index = _uiTexts;
        return index != null && index.TryTranslate(path, sourceText, out string translated)
            ? translated
            : sourceText;
    }

    internal bool MasterDataTranslationReady => _masterDataTranslator != null;

    internal int MasterDataTranslationTableCount => _masterDataTranslator?.TableCount ?? 0;

    internal bool HasMasterDataTranslation(Il2CppSystem.Type cacheType) =>
        _masterDataTranslator?.Contains(cacheType) == true;

    internal bool TryTranslateMasterDataCache(
        Il2CppSystem.Type cacheType,
        Absf.Master.IMasterLoadResult result,
        out int translatedEntries
    )
    {
        var translator = _masterDataTranslator;
        if (translator != null)
            return translator.TryTranslate(cacheType, result, out translatedEntries);

        translatedEntries = 0;
        return false;
    }

    internal bool HasNovel(string novelId) => _novels.ContainsKey(novelId);

    internal bool TryGetNovel(string novelId, out Dictionary<string, string> translations) =>
        _novels.TryGetValue(novelId, out translations);

    // ── 剧情翻译按需加载 ────────────────────

    public async Task GetNovelTranslationAsync(string novelId)
    {
        if (_novels.ContainsKey(novelId))
            return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = _loadingNovels.GetOrAdd(novelId, tcs.Task);
        if (existing != tcs.Task)
        {
            await existing;
            return;
        }

        try
        {
            var translations = await _cache.LoadAsync(TranslationPaths.Novels, novelId);
            if (translations != null)
            {
                _novels[novelId] = translations;
                Logger.Info($"Scenario translation loaded. Total: {translations.Count}");
            }
            else
            {
                Logger.Warn($"Translations loaded failed: {novelId}");
                Toast.Warning("加载失败", $"剧本ID: {novelId}");
            }
            tcs.SetResult();
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
            throw;
        }
        finally
        {
            _loadingNovels.TryRemove(novelId, out _);
        }
    }

    private static bool IsMasterDataStaticType(string type) =>
        type.StartsWith("m_", StringComparison.Ordinal);
}
