#nullable enable
using System;
using System.IO;
using System.Linq;

namespace AbyssMod.Services;

/// <summary>图片替换清单共用的扩展名、目录边界与远程路径规则。</summary>
internal static class ImageReplacementPaths
{
    internal const string ManifestFile = "manifest.json";

    internal static string ResolveImage(string root, string relativeFile)
    {
        if (string.IsNullOrWhiteSpace(relativeFile))
            throw new InvalidDataException("file is empty");
        if (
            Path.IsPathRooted(relativeFile)
            || Path.IsPathFullyQualified(relativeFile)
            || relativeFile.Contains(':')
            || relativeFile.IndexOf('\0') >= 0
        )
            throw new InvalidDataException("file must be a local relative path");
        if (!IsImage(relativeFile))
            throw new InvalidDataException("only PNG, JPG, and JPEG files are supported");
        return ResolveUnderRoot(root, relativeFile);
    }

    // 远程路径还要求每一段可直接编码成 URL；本地清单保留 ./ 等目录内相对写法。
    internal static string NormalizeRemoteFile(string relativeFile, bool allowManifest)
    {
        if (string.IsNullOrWhiteSpace(relativeFile))
            throw new InvalidDataException("replacement file path is empty");
        string normalized = relativeFile.Replace('\\', '/');
        if (
            normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains(':')
            || normalized.IndexOf('\0') >= 0
            || normalized.Split('/').Any(part => part is "" or "." or "..")
        )
            throw new InvalidDataException($"invalid replacement file path: {relativeFile}");
        if (
            allowManifest
            && string.Equals(normalized, ManifestFile, StringComparison.OrdinalIgnoreCase)
        )
            return ManifestFile;
        if (!IsImage(normalized))
            throw new InvalidDataException(
                $"unsupported replacement file extension: {relativeFile}"
            );
        return normalized;
    }

    internal static string ResolveUnderRoot(string root, string relativeFile)
    {
        string fullRoot = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(
            Path.Combine(fullRoot, relativeFile.Replace('/', Path.DirectorySeparatorChar))
        );
        string prefix =
            fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"replacement file escapes cache root: {relativeFile}");
        return candidate;
    }

    private static bool IsImage(string file)
    {
        string extension = Path.GetExtension(file);
        return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
    }
}
