using System;
using System.IO;

namespace AbyssMod.Services;

/// <summary>
/// 翻译资源路径构建工具。
/// 负责生成远程 URL 和本地缓存路径。
/// </summary>
public static class TranslationPaths
{
    public const string Manifest = "manifest";
    public const string Names = "names";
    public const string Titles = "titles";
    public const string Descriptions = "descriptions";
    public const string Novels = "novels";

    /// <summary>
    /// 构建远程资源 URL。
    /// </summary>
    /// <param name="cdn">CDN 根地址（已去除尾部斜杠）。</param>
    /// <param name="type">资源类型（manifest/names/words/novels）。</param>
    /// <param name="language">语言代码。</param>
    /// <param name="id">可选的资源 ID（如 novelId）。</param>
    /// <returns>完整的远程 URL。</returns>
    /// <exception cref="ArgumentException">当类型未知或 novels 类型缺少 ID 时抛出。</exception>
    public static string BuildRemoteUrl(string cdn, string type, string language, string id = null)
    {
        return type switch
        {
            Manifest => $"{cdn}/{Manifest}/{language}.json",
            Names => $"{cdn}/{Names}/{language}.json",
            Titles => $"{cdn}/{Titles}/{language}.json",
            Descriptions => $"{cdn}/{Descriptions}/{language}.json",
            Novels when id != null => $"{cdn}/{Novels}/{id}/{language}.json",
            Novels => throw new ArgumentException("Novel ID is required for novels type"),
            _ => throw new ArgumentException($"Unknown translation type: {type}"),
        };
    }

    /// <summary>
    /// 构建本地缓存文件路径。
    /// </summary>
    /// <param name="cacheDir">缓存根目录。</param>
    /// <param name="type">资源类型（manifest/names/words/novels）。</param>
    /// <param name="language">语言代码。</param>
    /// <param name="id">可选的资源 ID（如 novelId）。</param>
    /// <returns>完整的本地缓存路径。</returns>
    /// <exception cref="ArgumentException">当类型未知或 novels 类型缺少 ID 时抛出。</exception>
    public static string BuildCachePath(
        string cacheDir,
        string type,
        string language,
        string id = null
    )
    {
        var langDir = Path.Combine(cacheDir, language);
        return type switch
        {
            Manifest => Path.Combine(langDir, $"{Manifest}.json"),
            Names => Path.Combine(langDir, $"{Names}.json"),
            Titles => Path.Combine(langDir, $"{Titles}.json"),
            Descriptions => Path.Combine(langDir, $"{Descriptions}.json"),
            Novels when id != null => Path.Combine(langDir, Novels, $"{id}.json"),
            Novels => throw new ArgumentException("Novel ID is required for novels type"),
            _ => throw new ArgumentException($"Unknown translation type: {type}"),
        };
    }
}
