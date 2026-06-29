using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using Utility.Fonts;
using Utility.Toast;

namespace AbyssMod.Services;

/// <summary>
/// 翻译协调层：持有所有运行时翻译数据，提供统一查询入口。
/// </summary>
public class TranslationManager
{
    private readonly TranslationCache _cache;
    private readonly FontHelper _font;
    private readonly object _loadLock = new();
    private Task _loadTask;

    private readonly ConcurrentDictionary<string, Task> _loadingNovels = new();

    /// <summary>MasterData 字段级翻译表 { type: { field: { original: translated } } }。</summary>
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _tables =
        new();

    /// <summary>扁平翻译表 { type: { original: translated } }，供 UI / 剧情辅助查询。</summary>
    private readonly Dictionary<string, Dictionary<string, string>> _flatTables = new();

    /// <summary>剧情正文翻译表（按需懒加载，独立存放）。</summary>
    public ConcurrentDictionary<string, Dictionary<string, string>> Novels { get; } = new();

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
        _ = EnsureStaticTranslationsLoadedAsync();
    }

    // ── 静态翻译加载 ────────────────────────

    public Task EnsureStaticTranslationsLoadedAsync()
    {
        lock (_loadLock)
        {
            return _loadTask ??= LoadTranslationAsync();
        }
    }

    /// <summary>
    /// 同步等待静态翻译就绪。
    /// 仅在 MasterData 热路径上使用 — BepInEx IL2CPP 无 SynchronizationContext，
    /// 故 .GetAwaiter().GetResult() 不会死锁，但会阻塞调用线程直至 I/O 完成。
    /// </summary>
    public void EnsureStaticTranslationsLoaded()
    {
        EnsureStaticTranslationsLoadedAsync().GetAwaiter().GetResult();
    }

    private async Task LoadTranslationAsync()
    {
        if (!Config.Translation.Value)
            return;

        await _cache.FetchManifestAsync();

        var bundle = await _cache.LoadStaticBundleAsync();
        if (bundle != null)
        {
            int total = 0,
                loaded = 0,
                missing = 0;
            foreach (var type in MasterMapping.ContentTypes)
            {
                if (!IsMasterDataStaticType(type))
                    continue;
                if (bundle.TryGetValue(type, out var table) && table != null)
                {
                    _tables[type] = table;
                    total += CountEntries(table);
                    loaded++;
                    _flatTables[type] = FlattenFields(table);
                }
                else
                    missing++;
            }
            Logger.Info($"Static translation bundle loaded. Tables: {loaded}, Total: {total}");
            if (missing > 0)
                Logger.Warn($"Static translation bundle missing {missing} configured tables.");
        }
        else
        {
            Logger.Warn("MasterData static translation bundle load failed.");
            Toast.Warn("加载失败", "MasterData 静态翻译合并包加载失败");
        }

        await LoadFlatStaticTablesAsync();
    }

    private async Task LoadFlatStaticTablesAsync()
    {
        var tasks = new Dictionary<string, Task<Dictionary<string, string>>>();
        foreach (var type in MasterMapping.ContentTypes)
            if (!IsMasterDataStaticType(type))
                tasks[type] = _cache.LoadAsync(type);

        if (tasks.Count == 0)
            return;
        await Task.WhenAll(tasks.Values);

        foreach (var (type, task) in tasks)
        {
            var result = await task;
            if (result != null)
            {
                _flatTables[type] = result;
                Logger.Info($"Flat static translation loaded [{type}]. Total: {result.Count}");
            }
            else
                Logger.Warn($"Flat static translation load failed [{type}]");
        }
    }

    // ── 查询 API ────────────────────────────

    public Dictionary<string, string> GetTable(string type) =>
        _flatTables.TryGetValue(type, out var table) ? table : null;

    public Dictionary<string, string> GetFieldTable(string type, string field) =>
        _tables.TryGetValue(type, out var fields) && fields.TryGetValue(field, out var table)
            ? table
            : GetTable(type);

    // ── 剧情翻译按需加载 ────────────────────

    public async Task GetNovelTranslationAsync(string novelId)
    {
        if (Novels.ContainsKey(novelId))
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
                Novels[novelId] = translations;
                Logger.Info($"Scenario translation loaded. Total: {translations.Count}");
            }
            else
            {
                Logger.Warn($"Translations loaded failed: {novelId}");
                Toast.Warn("加载失败", $"剧本ID: {novelId}");
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

    // ── 私有辅助 ────────────────────────────

    private static int CountEntries(Dictionary<string, Dictionary<string, string>> fields)
    {
        int count = 0;
        foreach (var t in fields.Values)
            if (t != null)
                count += t.Count;
        return count;
    }

    private static Dictionary<string, string> FlattenFields(
        Dictionary<string, Dictionary<string, string>> fields
    )
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in fields.Values)
        {
            if (table == null)
                continue;
            foreach (var (k, v) in table)
                result[k] = v;
        }
        return result;
    }

    private static bool IsMasterDataStaticType(string type) =>
        type.StartsWith("m_", StringComparison.Ordinal);
}
