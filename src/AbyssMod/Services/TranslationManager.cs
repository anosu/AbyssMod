using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Utility.Notifications;

namespace AbyssMod.Services;

/// <summary>持有一个翻译会话；卸载取消请求，并阻止旧结果发布。</summary>
internal sealed class TranslationManager
{
    private readonly TranslationCache _cache;
    private readonly object _loadLock = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private readonly bool _translateMasterData;
    private readonly bool _translateUi;
    private Task _loadTask;
    private volatile bool _shutdown;
    private bool _staticTranslationsLoaded;
    private volatile MasterDataTranslator _masterDataTranslator;
    private volatile Dictionary<string, string> _names;
    private volatile UiTextIndex _uiTexts;
    private readonly ConcurrentDictionary<string, Task> _loadingNovels = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _novels = new();

    public TranslationManager(TranslationCache cache, bool translateMasterData, bool translateUi)
    {
        _cache = cache;
        _token = _lifetime.Token;
        _translateMasterData = translateMasterData;
        _translateUi = translateUi;
    }

    public void Shutdown()
    {
        lock (_loadLock)
        {
            if (_shutdown)
                return;
            _shutdown = true;
        }
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    public Task EnsureStaticTranslationsLoadedAsync()
    {
        lock (_loadLock)
        {
            if (_shutdown)
                return Task.CompletedTask;
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
        try
        {
            await _cache.FetchManifestAsync(_token).ConfigureAwait(false);
            var bundleTask =
                _translateMasterData && _masterDataTranslator == null
                    ? _cache.LoadStaticBundleAsync(_token)
                    : null;
            var namesTask =
                _names == null
                    ? _cache.LoadAsync(TranslationPaths.Names, cancellationToken: _token)
                    : null;
            var uiTask = _translateUi && _uiTexts == null ? _cache.LoadUiTextsAsync(_token) : null;
            // 观察全部请求，避免某一项失败后留下无人观察的并行任务。
            await Task.WhenAll(
                    (Task)bundleTask ?? Task.CompletedTask,
                    (Task)namesTask ?? Task.CompletedTask,
                    (Task)uiTask ?? Task.CompletedTask
                )
                .ConfigureAwait(false);
            lock (_loadLock)
            {
                if (_shutdown)
                    return;
                if (bundleTask != null)
                {
                    var bundle = bundleTask.GetAwaiter().GetResult();
                    if (bundle != null)
                    {
                        var tables =
                            new Dictionary<
                                string,
                                Dictionary<string, Dictionary<string, string>>
                            >();
                        foreach (var (type, table) in bundle)
                            if (type.StartsWith("m_", StringComparison.Ordinal) && table != null)
                                tables[type] = table;
                        _masterDataTranslator = MasterDataTranslator.Create(tables);
                        Logger.Info($"Static translation bundle loaded. Tables: {tables.Count}");
                    }
                    else
                        LogUnavailableStaticTranslation(TranslationPaths.Static);
                }
                if (namesTask != null)
                {
                    _names = namesTask.GetAwaiter().GetResult();
                    if (_names == null)
                        LogUnavailableStaticTranslation(TranslationPaths.Names);
                    else
                        Logger.Info($"Static translation loaded [names]. Total: {_names.Count}");
                }
                if (uiTask != null)
                {
                    var table = uiTask.GetAwaiter().GetResult();
                    if (table == null)
                        LogUnavailableStaticTranslation(TranslationPaths.UiTexts);
                    else
                    {
                        _uiTexts = new UiTextIndex(table);
                        Logger.Info($"Static translation loaded [ui_texts]. Paths: {table.Count}");
                    }
                }
                _staticTranslationsLoaded =
                    _names != null
                    && (!_translateMasterData || _masterDataTranslator != null)
                    && (!_translateUi || _uiTexts != null);
            }
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_shutdown)
                Logger.Warn($"Static translation load failed: {error}");
        }
    }

    public string TranslateName(string sourceText) =>
        !string.IsNullOrEmpty(sourceText)
        && _names != null
        && _names.TryGetValue(sourceText, out var translated)
        && !string.IsNullOrEmpty(translated)
            ? translated
            : sourceText;

    internal bool HasUiTranslations => _uiTexts != null;

    public string TranslateUiText(string path, string sourceText)
    {
        var index = _uiTexts;
        return index != null && index.TryTranslate(path, sourceText, out string translated)
            ? translated
            : sourceText;
    }

    internal bool MasterDataTranslationReady => _masterDataTranslator != null;

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

    public async Task GetNovelTranslationAsync(string novelId)
    {
        if (_shutdown || _novels.ContainsKey(novelId))
            return;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = _loadingNovels.GetOrAdd(novelId, tcs.Task);
        if (existing != tcs.Task)
        {
            await existing.ConfigureAwait(false);
            return;
        }
        try
        {
            var translations = await _cache
                .LoadAsync(TranslationPaths.Novels, novelId, _token)
                .ConfigureAwait(false);
            lock (_loadLock)
            {
                if (!_shutdown)
                {
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
                }
            }
            tcs.SetResult();
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested)
        {
            tcs.TrySetResult();
        }
        catch (Exception error)
        {
            tcs.SetException(error);
            // 返回共享任务，由所有调用方观察同一错误。
            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _loadingNovels.TryRemove(novelId, out _);
        }
    }

    private void LogUnavailableStaticTranslation(string type)
    {
        Logger.Warn($"Static translation load failed [{type}]");
        if (type == TranslationPaths.Static)
            Toast.Warning("加载失败", "MasterData 静态翻译合并包加载失败");
    }
}
