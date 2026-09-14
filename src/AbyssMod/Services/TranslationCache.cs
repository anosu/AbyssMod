using System;
using System.Buffers;
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

using UiTranslationTable = Dictionary<string, Dictionary<string, string>>;

public class TranslationCache
{
    private readonly string _cdn;
    private readonly string _cacheDir;
    private readonly string _language;
    private readonly bool _preferLocalFiles;
    private readonly HttpClient _client;
    private Manifest _manifest;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static readonly byte[] EntrySeparator = { 0 };

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
        _client = client;

        Directory.CreateDirectory(_cacheDir);
    }

    // ══ Manifest ═══════════════════════════════════════════════════════════

    public async Task FetchManifestAsync()
    {
        var relativePath = TranslationPaths.BuildRelativePath(TranslationPaths.Manifest, _language);
        var url = TranslationPaths.BuildRemoteUrl(_cdn, relativePath);
        var path = TranslationPaths.BuildCachePath(_cacheDir, relativePath);
        var cachedHash = TryReadManifestHash(path);

        try
        {
            using var response = await _client.GetAsync(url);
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

                    TryWriteTextFile(path, json);
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

    private async Task<T> LoadWithCacheAsync<T>(
        string relativePath,
        string expectedHash,
        Func<T, string> computeHash,
        bool fetchWhenUnlisted = false
    )
        where T : class
    {
        var remoteUrl = TranslationPaths.BuildRemoteUrl(_cdn, relativePath);
        var cachePath = TranslationPaths.BuildCachePath(_cacheDir, relativePath);
        var semaphore = _locks.GetOrAdd(relativePath, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            bool localExists = File.Exists(cachePath);
            T localData = null;
            if (_preferLocalFiles && localExists)
            {
                localData = LoadJsonFile<T>(cachePath, "Failed to load preferred local file");
                if (localData != null)
                {
                    Logger.Info($"Preferred local file: {relativePath}");
                    return localData;
                }
            }

            if (expectedHash != null && localExists)
            {
                localData ??= LoadJsonFile<T>(cachePath, "Failed to load cache");
                string localHash = TryComputeHash(localData, computeHash);
                if (HashesEqual(localHash, expectedHash))
                {
                    Logger.Info($"Cache hit: {relativePath}");
                    return localData;
                }
                Logger.Info(
                    $"Cache hash mismatch for {relativePath}, expected={expectedHash}, local={localHash}"
                );
                Logger.Info($"[翻译更新] CDN 有新版本内容: {relativePath}");
            }

            if (_manifest != null && expectedHash == null)
            {
                if (localExists)
                {
                    Logger.Info($"Using local file missing from manifest: {relativePath}");
                    return localData
                        ?? LoadJsonFile<T>(cachePath, "Failed to load unlisted local file");
                }

                if (!fetchWhenUnlisted)
                {
                    Logger.Info($"Manifest has no entry for {relativePath}, skipped.");
                    return null;
                }
            }

            Logger.Info($"Fetching from remote: {remoteUrl}");
            Logger.Info($"[下载翻译] 正在下载文件: {relativePath}");
            var data = await GetAsync<T>(remoteUrl);
            if (data != null)
            {
                if (expectedHash == null)
                {
                    TrySaveJsonFile(cachePath, data);
                    return data;
                }

                string remoteHash = TryComputeHash(data, computeHash);
                if (HashesEqual(remoteHash, expectedHash))
                {
                    TrySaveJsonFile(cachePath, data);
                    return data;
                }

                Logger.Warn(
                    $"Remote hash mismatch for {relativePath}, expected={expectedHash}, actual={remoteHash}"
                );
            }

            Logger.Warn($"Remote fetch failed for {relativePath}, trying local fallback.");
            if (File.Exists(cachePath))
            {
                data = localData ?? LoadJsonFile<T>(cachePath, "Failed to load cache");
                Logger.Info($"Loaded stale cache for {relativePath}");
            }
            return data;
        }
        finally
        {
            semaphore.Release();
        }
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
            using var response = await _client.GetAsync(url);
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

    private static string TryComputeHash<T>(T data, Func<T, string> computeHash)
        where T : class
    {
        if (data == null)
            return null;

        try
        {
            return computeHash(data);
        }
        catch (Exception e)
        {
            Logger.Warn($"Failed to hash translation data: {e.Message}");
            return null;
        }
    }

    private static bool HashesEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void TrySaveJsonFile<T>(string path, T data)
        where T : class => TryWriteTextFile(path, JsonSerializer.Serialize(data, JsonOptions));

    private static void TryWriteTextFile(string path, string content)
    {
        string tempPath = path + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(tempPath, content, Utf8);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to write translation cache {path}: {e.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch { }
        }
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

    private static string ComputeMd5Hex(IEnumerable<(string key, string value)> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        foreach (var (k, v) in entries)
        {
            AppendUtf8(hash, k);
            hash.AppendData(EntrySeparator);
            AppendUtf8(hash, v);
            hash.AppendData(EntrySeparator);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendUtf8(IncrementalHash hash, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        int byteCount = Utf8.GetByteCount(value);
        byte[] rented = null;
        Span<byte> buffer =
            byteCount <= 512
                ? stackalloc byte[byteCount]
                : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = Utf8.GetBytes(value.AsSpan(), buffer);
            hash.AppendData(buffer[..written]);
        }
        finally
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
