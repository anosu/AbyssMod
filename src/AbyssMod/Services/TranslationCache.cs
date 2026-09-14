using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Utility.Caching;
using Utility.Cryptography;

namespace AbyssMod.Services;

using UiTranslationTable = Dictionary<string, Dictionary<string, string>>;

public class TranslationCache
{
    private readonly string _cdn;
    private readonly string _cacheDir;
    private readonly string _language;
    private readonly bool _preferLocalFiles;
    private Manifest _manifest;

    private readonly JsonResourceCache _resources;

    public TranslationCache(
        string cdn,
        string cacheDir,
        string language,
        bool preferLocalFiles,
        HttpClient client
    )
    {
        _cdn = cdn.TrimEnd('/');
        _cacheDir = cacheDir;
        _language = language;
        _preferLocalFiles = preferLocalFiles;
        _resources = new JsonResourceCache(
            client,
            message => Logger.Info(message),
            message => Logger.Warn(message)
        );

        Directory.CreateDirectory(_cacheDir);
    }

    // ══ Manifest ═══════════════════════════════════════════════════════════

    public async Task FetchManifestAsync()
    {
        var relativePath = TranslationPaths.BuildRelativePath(TranslationPaths.Manifest, _language);
        var url = TranslationPaths.BuildRemoteUrl(_cdn, relativePath);
        var path = TranslationPaths.BuildCachePath(_cacheDir, relativePath);
        var previous = await _resources.LoadLocalAsync<Manifest>(path).ConfigureAwait(false);
        _manifest = await _resources
            .RefreshAsync<Manifest>(relativePath, path, url)
            .ConfigureAwait(false);
        if (_manifest != null)
        {
            if (!string.IsNullOrEmpty(previous?.Hash) && previous.Hash != _manifest.Hash)
                Logger.Info("[翻译更新] CDN 有新版本翻译内容");
            Logger.Info($"Manifest loaded ({_language}). Hash: {_manifest.Hash}");
        }
        else
            Logger.Warn("No manifest available, will fetch without hash verification.");
    }

    // ══ Public load API ════════════════════════════════════════════════════

    public Task<Dictionary<string, string>> LoadAsync(string type, string id = null)
    {
        return LoadWithCacheAsync<Dictionary<string, string>>(
            TranslationPaths.BuildRelativePath(type, _language, id),
            GetManifestHash(type, id),
            GetHash
        );
    }

    public Task<UiTranslationTable> LoadUiTextsAsync()
    {
        string type = TranslationPaths.UiTexts;
        return LoadWithCacheAsync<UiTranslationTable>(
            TranslationPaths.BuildRelativePath(type, _language),
            GetManifestHash(type, null),
            GetUiTextHash
        );
    }

    public Task<
        Dictionary<string, Dictionary<string, Dictionary<string, string>>>
    > LoadStaticBundleAsync()
    {
        string type = TranslationPaths.Static;
        return LoadWithCacheAsync<
            Dictionary<string, Dictionary<string, Dictionary<string, string>>>
        >(
            TranslationPaths.BuildRelativePath(type, _language),
            GetManifestHash(type, null),
            GetBundleHash,
            fetchWhenUnlisted: true
        );
    }

    // ══ Common cache-then-fetch flow ═══════════════════════════════════════

    private Task<T> LoadWithCacheAsync<T>(
        string relativePath,
        string expectedHash,
        Func<T, string> computeHash,
        bool fetchWhenUnlisted = false
    )
        where T : class
    {
        var remoteUrl = TranslationPaths.BuildRemoteUrl(_cdn, relativePath);
        var cachePath = TranslationPaths.BuildCachePath(_cacheDir, relativePath);
        var policy = _preferLocalFiles ? JsonCachePolicy.PreferLocal : JsonCachePolicy.Refresh;
        if (
            _manifest != null
            && expectedHash == null
            && (!fetchWhenUnlisted || File.Exists(cachePath))
        )
            policy = JsonCachePolicy.LocalOnly;
        return _resources.RefreshAsync<T>(
            relativePath,
            cachePath,
            remoteUrl,
            policy,
            expectedHash == null
                ? null
                : value =>
                    string.Equals(
                        computeHash(value),
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase
                    )
        );
    }

    // ══ Manifest hash ══════════════════════════════════════════════════════

    private string GetManifestHash(string type, string id)
    {
        if (_manifest == null)
            return null;
        if (type == TranslationPaths.Novels && id != null)
            return _manifest.Novels?.TryGetValue(id, out var hash) == true ? hash : null;
        return _manifest.GetFileHash(type);
    }

    // ══ Normalized hashing (Python-compatible) ═════════════════════════════

    private static string GetHash(Dictionary<string, string> dict)
    {
        if (dict == null)
            return null;
        return ComputeMd5Hex(
            dict.Keys.OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => ((string, string))(k, dict[k]))
        );
    }

    private static string GetBundleHash(
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> bundle
    )
    {
        if (bundle == null)
            return null;

        return ComputeMd5Hex(EnumerateBundleEntries(bundle));
    }

    private static string GetUiTextHash(UiTranslationTable tables) =>
        tables == null ? null : ComputeMd5Hex(EnumerateUiTextEntries(tables));

    private static IEnumerable<(string key, string value)> EnumerateUiTextEntries(
        UiTranslationTable tables
    )
    {
        foreach (var path in tables.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var translations = tables[path];
            if (translations == null)
                continue;
            foreach (var source in translations.Keys.OrderBy(k => k, StringComparer.Ordinal))
                yield return ($"{path}\x01{source}", translations[source]);
        }
    }

    private static IEnumerable<(string key, string value)> EnumerateBundleEntries(
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> bundle
    )
    {
        foreach (var type in bundle.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var fields = bundle[type];
            if (fields == null)
                continue;
            foreach (var field in fields.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var dict = fields[field];
                if (dict == null)
                    continue;
                foreach (var key in dict.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    yield return ($"{type}\x01{field}\x01{key}", dict[key]);
            }
        }
    }

    private static string ComputeMd5Hex(IEnumerable<(string key, string value)> entries) =>
        StringTableHash.ComputeEntries(entries);
}
