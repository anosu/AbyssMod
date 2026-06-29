using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AbyssMod.Services;

public class TranslationCache
{
    private readonly string _cdn;
    private readonly string _cacheDir;
    private readonly string _language;
    private readonly HttpClient _client;
    private Manifest _manifest;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private int _lockCleanupCounter;
    private const int LockCleanupInterval = 32;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public TranslationCache(string cdn, string cacheDir, string language, HttpClient client)
    {
        _cdn = cdn.TrimEnd('/');
        _cacheDir = cacheDir;
        _language = language;
        _client = client;

        var langDir = Path.Combine(_cacheDir, _language);
        Directory.CreateDirectory(langDir);
        Directory.CreateDirectory(Path.Combine(langDir, TranslationPaths.Novels));
    }

    public Manifest Manifest => _manifest;

    // ══ Manifest ═══════════════════════════════════════════════════════════

    public async Task FetchManifestAsync()
    {
        var url = TranslationPaths.BuildRemoteUrl(_cdn, TranslationPaths.Manifest, _language);
        var path = TranslationPaths.BuildCachePath(_cacheDir, TranslationPaths.Manifest, _language);
        var cachedHash = TryReadManifestHash(path);

        try
        {
            var response = await _client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _manifest = JsonSerializer.Deserialize<Manifest>(json);
                if (_manifest == null)
                {
                    Logger.Warn("Remote manifest parse returned null");
                }
                else
                {
                    if (
                        !string.IsNullOrEmpty(cachedHash)
                        && !string.Equals(cachedHash, _manifest.Hash, StringComparison.Ordinal)
                    )
                        Logger.Info("[翻译更新] CDN 有新版本翻译内容");

                    await File.WriteAllTextAsync(path, json, Utf8);
                    Logger.Info($"Manifest loaded ({_language}). Hash: {_manifest.Hash}");
                    return;
                }
            }
            Logger.Warn($"Manifest fetch returned {response.StatusCode}");
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to fetch manifest: {e.Message}");
        }

        TryLoadLocalManifest(path);
    }

    private void TryLoadLocalManifest(string path)
    {
        if (!File.Exists(path))
        {
            Logger.Warn("No local manifest cache available, will fetch without hash verification.");
            return;
        }

        try
        {
            var json = File.ReadAllText(path, Utf8);
            _manifest = JsonSerializer.Deserialize<Manifest>(json);
            if (_manifest != null)
                Logger.Info(
                    $"Loaded cached manifest from local ({_language}). Hash: {_manifest.Hash}"
                );
            else
                Logger.Warn("Cached manifest parse returned null");
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to load local manifest: {e.Message}");
        }
    }

    // ══ Public load API ════════════════════════════════════════════════════

    public async Task<Dictionary<string, string>> LoadAsync(string type, string id = null)
    {
        string cacheKey = id != null ? $"{_language}/{type}/{id}" : $"{_language}/{type}";
        string expectedHash = GetManifestHash(type, id);

        if (_manifest != null && expectedHash == null)
        {
            Logger.Info($"Manifest has no entry for {cacheKey}, skipped.");
            return new Dictionary<string, string>();
        }

        return await LoadWithCacheAsync<Dictionary<string, string>>(
            cacheKey,
            TranslationPaths.BuildRemoteUrl(_cdn, type, _language, id),
            TranslationPaths.BuildCachePath(_cacheDir, type, _language, id),
            expectedHash,
            HashFile
        );
    }

    public async Task<
        Dictionary<string, Dictionary<string, Dictionary<string, string>>>
    > LoadStaticBundleAsync()
    {
        string type = TranslationPaths.Static;
        string cacheKey = $"{_language}/{type}";
        string expectedHash = GetManifestHash(type, null);

        if (_manifest != null && expectedHash == null)
            Logger.Info(
                "Manifest has no static bundle entry; fetching bundle without hash verification."
            );

        return await LoadWithCacheAsync<
            Dictionary<string, Dictionary<string, Dictionary<string, string>>>
        >(
            cacheKey,
            TranslationPaths.BuildRemoteUrl(_cdn, type, _language),
            TranslationPaths.BuildCachePath(_cacheDir, type, _language),
            expectedHash,
            HashBundleFile
        );
    }

    // ══ Common cache-then-fetch flow ═══════════════════════════════════════

    private async Task<T> LoadWithCacheAsync<T>(
        string cacheKey,
        string remoteUrl,
        string cachePath,
        string expectedHash,
        Func<string, string> computeFileHash
    )
        where T : class
    {
        var semaphore = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            if (expectedHash != null && File.Exists(cachePath))
            {
                string localHash = computeFileHash(cachePath);
                if (localHash == expectedHash)
                {
                    Logger.Info($"Cache hit: {cacheKey}");
                    return LoadJsonFile<T>(cachePath, "Failed to load cache");
                }
                Logger.Info(
                    $"Cache hash mismatch for {cacheKey}, expected={expectedHash}, local={localHash}"
                );
                Logger.Info($"[翻译更新] CDN 有新版本内容: {cacheKey}");
            }

            Logger.Info($"Fetching from remote: {remoteUrl}");
            Logger.Info($"[下载翻译] 正在下载文件: {cacheKey}");
            var data = await GetAsync<T>(remoteUrl);
            if (data != null)
            {
                SaveJsonFile(cachePath, data);
                return data;
            }

            Logger.Warn($"Remote fetch failed for {cacheKey}, trying local fallback.");
            if (File.Exists(cachePath))
            {
                data = LoadJsonFile<T>(cachePath, "Failed to load cache");
                Logger.Info($"Loaded stale cache for {cacheKey}");
            }
            return data;
        }
        finally
        {
            semaphore.Release();
            CleanupLocksIfNeeded();
        }
    }

    // ══ Concurrency ════════════════════════════════════════════════════════

    private void CleanupLocksIfNeeded()
    {
        if (++_lockCleanupCounter % LockCleanupInterval != 0)
            return;

        foreach (var kvp in _locks)
            if (kvp.Value.CurrentCount > 0 && _locks.TryRemove(kvp.Key, out var sem))
                sem.Dispose();
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

    // ══ HTTP ═══════════════════════════════════════════════════════════════

    private async Task<T> GetAsync<T>(string url)
        where T : class
    {
        try
        {
            var response = await _client.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception e)
        {
            Logger.Error($"HTTP GET error for {url}: {e.Message}");
        }
        return null;
    }

    // ══ JSON file I/O ══════════════════════════════════════════════════════

    private static string TryReadManifestHash(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path, Utf8))?.Hash;
        }
        catch
        {
            return null;
        }
    }

    private static T LoadJsonFile<T>(string path, string errorPrefix)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Utf8));
        }
        catch (Exception e)
        {
            Logger.Error($"{errorPrefix} {path}: {e.Message}");
            return null;
        }
    }

    private static void SaveJsonFile<T>(string path, T data)
        where T : class
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions), Utf8);
    }

    // ══ Normalized hashing (Python-compatible) ═════════════════════════════

    private static string HashFile(string path) =>
        GetHash(
            JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Utf8))
        );

    private static string HashBundleFile(string path)
    {
        try
        {
            return GetBundleHash(
                JsonSerializer.Deserialize<
                    Dictionary<string, Dictionary<string, Dictionary<string, string>>>
                >(File.ReadAllText(path, Utf8))
            );
        }
        catch (Exception e)
        {
            Logger.Warn($"Static bundle cache is incompatible, refreshing: {e.Message}");
            return null;
        }
    }

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

        var entries = new List<(string key, string value)>();
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
                    entries.Add(($"{type}\x01{field}\x01{key}", dict[key]));
            }
        }
        return ComputeMd5Hex(entries);
    }

    private static string ComputeMd5Hex(IEnumerable<(string key, string value)> entries)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in entries)
        {
            sb.Append(k);
            sb.Append('\0');
            sb.Append(v);
            sb.Append('\0');
        }
        return Convert.ToHexString(MD5.HashData(Utf8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}
