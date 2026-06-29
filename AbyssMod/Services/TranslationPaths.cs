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
    public const string Novels = "novels";
    public const string Static = "static";
    public const string UiTexts = "ui_texts";

    /// <summary>
    /// 构建远程资源 URL。
    /// </summary>
    /// <param name="cdn">CDN 根地址（已去除尾部斜杠）。</param>
    /// <param name="type">资源类型。</param>
    /// <param name="language">语言代码。</param>
    /// <param name="id">可选的资源 ID（仅 novels 需要）。</param>
    /// <returns>完整的远程 URL。</returns>
    public static string BuildRemoteUrl(string cdn, string type, string language, string id = null)
    {
        return type switch
        {
            Novels when id == null => throw new ArgumentException(
                "Novel ID is required for novels type"
            ),
            Novels => $"{cdn}/{Novels}/{id}/{language}.json",
            Manifest => $"{cdn}/{Manifest}/{language}.json",
            _ => $"{cdn}/{type}/{language}.json",
        };
    }

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
            Novels when id == null => throw new ArgumentException(
                "Novel ID is required for novels type"
            ),
            Novels => Path.Combine(langDir, Novels, $"{id}.json"),
            Manifest => Path.Combine(langDir, $"{Manifest}.json"),
            _ => Path.Combine(langDir, $"{type}.json"),
        };
    }
}
