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
    public const string Novels = "novels";
    public const string Static = "static";
    public const string UiTexts = "ui_texts";

    /// <summary>构建资源在翻译仓库中的相对路径。</summary>
    public static string BuildRelativePath(string type, string language, string id = null)
    {
        return type switch
        {
            Novels when id == null => throw new ArgumentException(
                "Novel ID is required for novels type"
            ),
            Novels => $"{Novels}/{id}/{language}.json",
            Manifest => $"{Manifest}/{language}.json",
            _ => $"{type}/{language}.json",
        };
    }

    public static string BuildRemoteUrl(string cdn, string relativePath) =>
        $"{cdn.TrimEnd('/')}/{relativePath}";

    public static string BuildCachePath(string cacheDir, string relativePath)
    {
        return Path.Combine(cacheDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
