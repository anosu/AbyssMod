using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

    /// <summary>UI 翻译表及其原文候选索引，加载完成后整体替换。</summary>
    private UiTextIndex _uiTexts;

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

        await Task.WhenAll(LoadFlatStaticTablesAsync(), LoadUiTextsAsync());
    }

    private async Task LoadUiTextsAsync()
    {
        var result = await _cache.LoadUiTextsAsync();
        if (result != null)
        {
            _uiTexts = new UiTextIndex(result);
            Logger.Info($"Contextual UI text translation loaded. Total: {CountEntries(result)}");
        }
        else
            Logger.Warn("Contextual UI text translation load failed.");
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

    public bool TryTranslateUiText(
        string transformPath,
        string sourceText,
        out string translatedText
    )
    {
        translatedText = null;
        var index = _uiTexts;
        return index != null
            && index.TryTranslate(transformPath, sourceText, out translatedText);
    }

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

    private sealed class UiTextIndex
    {
        private static readonly Regex PlaceholderRegex = new(@"\{(\d+)\}", RegexOptions.Compiled);
        private readonly Dictionary<string, UiTextPathRules> _exactPaths = new(
            StringComparer.Ordinal
        );
        private readonly List<UiTextPathRules> _wildcardPaths = new();

        public UiTextIndex(Dictionary<string, Dictionary<string, string>> table)
        {
            foreach (var (path, translations) in table)
            {
                if (translations == null)
                    continue;

                var rules = new UiTextPathRules(path, translations);
                if (rules.IsWildcard)
                    _wildcardPaths.Add(rules);
                else
                    _exactPaths[path] = rules;
            }

            _wildcardPaths.Sort(
                static (left, right) => right.Specificity.CompareTo(left.Specificity)
            );
        }

        public bool TryTranslate(string transformPath, string sourceText, out string translatedText)
        {
            translatedText = null;
            if (
                string.IsNullOrEmpty(transformPath)
                || string.IsNullOrEmpty(sourceText)
            )
                return false;

            if (
                _exactPaths.TryGetValue(transformPath, out var exactRules)
                && exactRules.TryTranslate(sourceText, out translatedText)
            )
                return true;

            foreach (var rules in _wildcardPaths)
            {
                if (
                    rules.MatchesPath(transformPath)
                    && rules.TryTranslate(sourceText, out translatedText)
                )
                    return true;
            }

            return false;
        }

        private sealed class UiTextPathRules
        {
            private readonly Regex _pathRegex;
            private readonly Dictionary<string, string> _exactTexts = new(
                StringComparer.Ordinal
            );
            private readonly List<UiTextPattern> _patterns = new();

            public UiTextPathRules(
                string path,
                Dictionary<string, string> translations
            )
            {
                Path = path;
                IsWildcard = path.Contains('*', StringComparison.Ordinal);
                Specificity = path.Length - path.Count(character => character == '*');
                if (IsWildcard)
                    _pathRegex = new Regex(
                        BuildPathPattern(path),
                        RegexOptions.Compiled | RegexOptions.CultureInvariant
                    );

                foreach (var (sourceText, translatedText) in translations)
                {
                    if (string.IsNullOrEmpty(translatedText))
                        continue;

                    if (PlaceholderRegex.IsMatch(sourceText))
                        _patterns.Add(new UiTextPattern(sourceText, translatedText));
                    else
                        _exactTexts[sourceText] = translatedText;
                }
            }

            public string Path { get; }
            public bool IsWildcard { get; }
            public int Specificity { get; }

            public bool MatchesPath(string transformPath) =>
                !IsWildcard || _pathRegex.IsMatch(transformPath);

            public bool TryTranslate(string sourceText, out string translatedText)
            {
                if (_exactTexts.TryGetValue(sourceText, out translatedText))
                    return true;

                foreach (var pattern in _patterns)
                    if (pattern.TryTranslate(sourceText, out translatedText))
                        return true;

                translatedText = null;
                return false;
            }

            private static string BuildPathPattern(string path)
            {
                var pattern = new StringBuilder("^");
                foreach (char character in path)
                    pattern.Append(
                        character == '*'
                            ? "[^/]*"
                            : Regex.Escape(character.ToString())
                    );
                pattern.Append('$');
                return pattern.ToString();
            }
        }

        private sealed class UiTextPattern
        {
            private readonly Regex _sourceRegex;
            private readonly string _translatedTemplate;

            public UiTextPattern(string sourceTemplate, string translatedTemplate)
            {
                _sourceRegex = new Regex(
                    BuildSourcePattern(sourceTemplate),
                    RegexOptions.Compiled
                        | RegexOptions.CultureInvariant
                        | RegexOptions.Singleline
                );
                _translatedTemplate = translatedTemplate;
            }

            public bool TryTranslate(string sourceText, out string translatedText)
            {
                var match = _sourceRegex.Match(sourceText);
                if (!match.Success)
                {
                    translatedText = null;
                    return false;
                }

                translatedText = PlaceholderRegex.Replace(
                    _translatedTemplate,
                    matchResult =>
                    {
                        string groupName = $"p{matchResult.Groups[1].Value}";
                        return match.Groups[groupName].Success
                            ? match.Groups[groupName].Value
                            : matchResult.Value;
                    }
                );
                return !string.IsNullOrEmpty(translatedText);
            }

            private static string BuildSourcePattern(string sourceTemplate)
            {
                var pattern = new StringBuilder("^");
                var seenPlaceholders = new HashSet<string>(StringComparer.Ordinal);
                int lastIndex = 0;
                foreach (Match match in PlaceholderRegex.Matches(sourceTemplate))
                {
                    pattern.Append(
                        Regex.Escape(sourceTemplate.Substring(lastIndex, match.Index - lastIndex))
                    );

                    string groupName = $"p{match.Groups[1].Value}";
                    pattern.Append(
                        seenPlaceholders.Add(groupName)
                            ? $"(?<{groupName}>.+?)"
                            : $"\\k<{groupName}>"
                    );
                    lastIndex = match.Index + match.Length;
                }

                pattern.Append(Regex.Escape(sourceTemplate.Substring(lastIndex)));
                pattern.Append('$');
                return pattern.ToString();
            }
        }
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
