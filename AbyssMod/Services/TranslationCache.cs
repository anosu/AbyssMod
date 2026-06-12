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
using Utility.Toast;

namespace AbyssMod.Services
{
    /// <summary>
    /// 基于清单（Manifest）的翻译缓存管理器，支持本地持久化。
    ///
    /// <para>使用示例：</para>
    /// <code>
    ///   await cache.LoadAsync("names");
    ///   await cache.LoadAsync("novels", "10005");
    /// </code>
    ///
    /// <para>加载流程：</para>
    /// <list type="number">
    ///   <item>检查清单中是否存在资源哈希</item>
    ///   <item>如果本地缓存文件存在，计算其规范化哈希值</item>
    ///   <item>哈希匹配 → 使用本地缓存</item>
    ///   <item>哈希不匹配或不存在 → 从远程获取并保存到本地缓存</item>
    /// </list>
    /// </summary>
    public class TranslationCache
    {
        private readonly string _cdn;
        private readonly string _cacheDir;
        private readonly string _language;
        private readonly HttpClient _client;
        private Manifest _manifest;

        /// <summary>防止同一资源并发加载的锁集合。</summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        /// <summary>锁清理计数器，用于定期清理无引用的锁。</summary>
        private int _lockCleanupCounter;

        private const int LockCleanupInterval = 32;

        /// <summary>JSON 序列化选项（用于保存缓存文件）。</summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        /// <summary>UTF-8 编码（无 BOM），用于所有文件 I/O。</summary>
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>
        /// 初始化翻译缓存管理器。
        /// </summary>
        /// <param name="cdn">CDN 根地址。</param>
        /// <param name="cacheDir">本地缓存根目录。</param>
        /// <param name="language">目标语言代码。</param>
        /// <param name="client">HTTP 客户端实例。</param>
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

        /// <summary>获取当前已加载的翻译清单。</summary>
        public Manifest Manifest => _manifest;

        /// <summary>
        /// 获取并缓存远程翻译清单。
        /// 失败时回退到本地已缓存的清单。
        /// </summary>
        public async Task FetchManifestAsync()
        {
            var url = TranslationPaths.BuildRemoteUrl(_cdn, TranslationPaths.Manifest, _language);
            var path = TranslationPaths.BuildCachePath(
                _cacheDir,
                TranslationPaths.Manifest,
                _language
            );

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

            // 回退：尝试加载本地缓存的清单
            TryLoadLocalManifest(path);
        }

        /// <summary>
        /// 尝试从磁盘加载之前缓存的清单文件。
        /// </summary>
        private void TryLoadLocalManifest(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Warn(
                    "No local manifest cache available, will fetch without hash verification."
                );
                Toast.Warn("翻译服务", "翻译清单不可用，将直接请求翻译");
                return;
            }

            try
            {
                var json = File.ReadAllText(path, Utf8);
                _manifest = JsonSerializer.Deserialize<Manifest>(json);
                if (_manifest != null)
                {
                    Logger.Info(
                        $"Loaded cached manifest from local ({_language}). Hash: {_manifest.Hash}"
                    );
                    Toast.Warn("翻译服务", "无法连接远程，使用本地翻译清单");
                }
                else
                {
                    Logger.Warn("Cached manifest parse returned null");
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to load local manifest: {e.Message}");
            }
        }

        /// <summary>
        /// 加载翻译数据，支持缓存感知逻辑。
        /// </summary>
        /// <param name="type">翻译类型：names、words 或 novels。</param>
        /// <param name="id">可选标识符（如 novels 的 novelId）。</param>
        /// <returns>加载的字典，失败时返回 null。</returns>
        public async Task<Dictionary<string, string>> LoadAsync(string type, string id = null)
        {
            string cacheKey = id != null ? $"{_language}/{type}/{id}" : $"{_language}/{type}";
            string remoteUrl = TranslationPaths.BuildRemoteUrl(_cdn, type, _language, id);
            string cachePath = TranslationPaths.BuildCachePath(_cacheDir, type, _language, id);
            string expectedHash = GetManifestHash(type, id);

            // 序列化同一资源的并发加载
            var semaphore = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                // 如果清单中有预期哈希值，先检查本地缓存
                if (expectedHash != null && File.Exists(cachePath))
                {
                    string localHash = HashFile(cachePath);
                    if (localHash == expectedHash)
                    {
                        Logger.Info($"Cache hit: {cacheKey}");
                        return LoadFromFile(cachePath);
                    }
                    Logger.Info(
                        $"Cache hash mismatch for {cacheKey}, "
                            + $"expected={expectedHash}, local={localHash}"
                    );
                }

                // 从远程获取
                Logger.Info($"Fetching from remote: {remoteUrl}");
                var data = await GetAsync<Dictionary<string, string>>(remoteUrl);
                if (data != null)
                {
                    SaveToFile(cachePath, data);
                }
                else
                {
                    // 远程获取失败 → 回退到本地缓存（即使哈希不匹配）
                    Logger.Warn($"Remote fetch failed for {cacheKey}, trying local fallback.");
                    if (File.Exists(cachePath))
                    {
                        data = LoadFromFile(cachePath);
                        Logger.Info($"Loaded stale cache for {cacheKey}");
                    }
                }
                return data;
            }
            finally
            {
                semaphore.Release();
                CleanupLocksIfNeeded();
            }
        }

        /// <summary>
        /// 定期移除不再竞争的 SemaphoreSlim 条目。
        /// 防止 _locks 字典无限增长。
        /// </summary>
        private void CleanupLocksIfNeeded()
        {
            if (++_lockCleanupCounter % LockCleanupInterval != 0)
                return;

            // 仅移除无等待者的条目 - 安全删除空闲信号量
            var keysToRemove = new List<string>();
            foreach (var kvp in _locks)
            {
                if (kvp.Value.CurrentCount > 0) // 无活动等待者
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
            {
                if (_locks.TryRemove(key, out var sem) && sem.CurrentCount > 0)
                    sem.Dispose();
            }
        }

        /// <summary>
        /// 从清单中获取指定类型/ID 组合的预期哈希值。
        /// </summary>
        /// <returns>清单不包含此资源时返回 null。</returns>
        private string GetManifestHash(string type, string id)
        {
            if (_manifest == null)
                return null;
            return type switch
            {
                TranslationPaths.Names => _manifest.Names,
                TranslationPaths.Titles => _manifest.Titles,
                TranslationPaths.Descriptions => _manifest.Descriptions,
                TranslationPaths.Novels when id != null => _manifest.Novels != null
                && _manifest.Novels.TryGetValue(id, out var hash)
                    ? hash
                    : null,
                _ => null,
            };
        }

        /// <summary>
        /// 发起 HTTP GET 请求并反序列化 JSON 响应。
        /// </summary>
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

        /// <summary>
        /// 从本地文件加载翻译字典。
        /// </summary>
        private static Dictionary<string, string> LoadFromFile(string path)
        {
            try
            {
                var json = File.ReadAllText(path, Utf8);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to load translation cache {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将翻译字典保存到本地文件。
        /// </summary>
        private static void SaveToFile(string path, Dictionary<string, string> data)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(path, json, Utf8);
        }

        /// <summary>
        /// 计算翻译 JSON 文件的规范化哈希值。
        /// </summary>
        private static string HashFile(string path)
        {
            var json = File.ReadAllText(path, Utf8);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return GetHash(dict);
        }

        /// <summary>
        /// 计算字典的规范化哈希值（与 Python 脚本兼容）。
        /// </summary>
        private static string GetHash(Dictionary<string, string> dict)
        {
            if (dict == null)
                return null;

            var sb = new StringBuilder();
            foreach (var key in dict.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                sb.Append(key);
                sb.Append('\0');
                sb.Append(dict[key]);
                sb.Append('\0');
            }

            var hash = MD5.HashData(Utf8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
