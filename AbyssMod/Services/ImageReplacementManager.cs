using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssMod.Services;

/// <summary>加载本地图片，并按 manifest 规则为游戏 Sprite 创建替代对象。</summary>
public sealed class ImageReplacementManager : IDisposable
{
    private const int SupportedManifestVersion = 1;
    private const int MaxImageDimension = 8192;
    private const long MaxImagePixels = 16L * 1024 * 1024;
    private const long MaxManifestBytes = 1024 * 1024;
    private const long MaxImageBytes = 64L * 1024 * 1024;
    private readonly string _replacementRoot;
    private readonly string _manifestPath;
    private readonly Dictionary<string, string> _novelBackgrounds =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _spriteNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<UiRule>> _uiComponents =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _uiSourceSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> _textureCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<LogicalCanvasKey, Texture2D> _logicalCanvasCache = new();
    private readonly Dictionary<SpriteCacheKey, Sprite> _spriteCache = new();
    private readonly HashSet<int> _replacementSpriteIds = new();
    private readonly Dictionary<int, string> _replacementSpriteFiles = new();
    private readonly HashSet<string> _failedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<LogicalCanvasKey> _failedCanvases = new();
    private readonly HashSet<SpriteCacheKey> _failedSprites = new();
    private readonly HashSet<DimensionWarningKey> _dimensionWarnings = new();
    private bool _hasUnscopedUiRules;
    private bool _disposed;

    public ImageReplacementManager(string replacementRoot)
    {
        _replacementRoot = Path.GetFullPath(replacementRoot);
        _manifestPath = Path.Combine(_replacementRoot, "manifest.json");
    }

    public bool Enabled =>
        _novelBackgrounds.Count > 0
        || _spriteNames.Count > 0
        || _uiComponents.Count > 0;

    public void Initialize()
    {
        if (!File.Exists(_manifestPath))
        {
            Logger.Info($"Image replacement manifest not found: {_manifestPath}");
            return;
        }

        try
        {
            var manifestFile = new FileInfo(_manifestPath);
            if (manifestFile.Length > MaxManifestBytes)
                throw new InvalidDataException(
                    $"manifest exceeds {MaxManifestBytes / 1024} KiB"
                );

            string json = File.ReadAllText(_manifestPath);
            var manifest = JsonSerializer.Deserialize<ImageReplacementManifest>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                }
            );
            if (manifest == null)
                throw new InvalidDataException("manifest root is null");
            if (manifest.Version != SupportedManifestVersion)
                throw new InvalidDataException(
                    $"unsupported manifest version {manifest.Version}; expected {SupportedManifestVersion}"
                );

            AddMappings(manifest.NovelBackgrounds, _novelBackgrounds, "novelBackgrounds");
            AddMappings(manifest.SpriteNames, _spriteNames, "spriteNames");
            AddUiRules(manifest.UiComponents);

            Logger.Info(
                "Image replacement manifest loaded: "
                    + $"novel={_novelBackgrounds.Count}, "
                    + $"spriteName={_spriteNames.Count}, "
                    + $"ui={CountUiRules()}"
            );
        }
        catch (Exception e)
        {
            ClearRules();
            Logger.Error($"Image replacement manifest load failed: {e.Message}");
        }
    }

    public Sprite ReplaceNovelBackground(string id, Sprite original)
    {
        if (!CanReplace(original))
            return original;

        if (
            !string.IsNullOrEmpty(id)
            && _novelBackgrounds.TryGetValue(id, out string file)
        )
            return GetOrCreateSprite(file, original);
        if (TryMatchSpriteName(original, out file))
            return GetOrCreateSprite(file, original);

        return original;
    }

    public Sprite ReplaceUiImage(Image image, Sprite original)
    {
        if (_disposed || original == null)
            return original;

        int originalId = original.GetInstanceID();
        bool isReplacement = _replacementSpriteFiles.TryGetValue(
            originalId,
            out string originalReplacementFile
        );
        if (
            _uiComponents.Count > 0
            && (_hasUnscopedUiRules || _uiSourceSprites.Contains(original.name))
        )
        {
            string transformPath = GetTransformPath(image?.transform);
            if (
                transformPath != null
                && _uiComponents.TryGetValue(transformPath, out var rules)
                && TryMatchUiRule(rules, original, out string uiFile)
            )
            {
                if (
                    isReplacement
                    && string.Equals(
                        originalReplacementFile,
                        uiFile,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return original;
                return GetOrCreateSprite(uiFile, original);
            }
        }

        if (isReplacement)
            return original;

        return ReplaceBySpriteName(original);
    }

    public Sprite ReplaceBySpriteName(Sprite original)
    {
        if (
            _spriteNames.Count > 0
            && CanReplace(original)
            && TryMatchSpriteName(original, out string file)
        )
            return GetOrCreateSprite(file, original);

        return original;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var sprite in _spriteCache.Values)
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
        }
        foreach (var texture in _textureCache.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }
        foreach (var texture in _logicalCanvasCache.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _spriteCache.Clear();
        _textureCache.Clear();
        _logicalCanvasCache.Clear();
        _replacementSpriteIds.Clear();
        _replacementSpriteFiles.Clear();
    }

    private bool CanReplace(Sprite original)
    {
        if (_disposed || original == null)
            return false;
        return !_replacementSpriteIds.Contains(original.GetInstanceID());
    }

    private void AddMappings(
        Dictionary<string, string> source,
        Dictionary<string, string> destination,
        string section
    )
    {
        if (source == null)
            return;

        foreach (var (key, relativeFile) in source)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Logger.Warn($"Image replacement [{section}] skipped an empty key.");
                continue;
            }
            if (!TryResolveImagePath(relativeFile, out string fullPath, out string error))
            {
                Logger.Warn($"Image replacement [{section}] '{key}' skipped: {error}");
                continue;
            }
            destination[key] = fullPath;
        }
    }

    private void AddUiRules(List<UiImageReplacement> rules)
    {
        if (rules == null)
            return;

        foreach (var rule in rules)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.TransformPath))
            {
                Logger.Warn("Image replacement [uiComponents] skipped an empty transformPath.");
                continue;
            }
            if (!TryResolveImagePath(rule.File, out string fullPath, out string error))
            {
                Logger.Warn(
                    $"Image replacement [uiComponents] '{rule.TransformPath}' skipped: {error}"
                );
                continue;
            }

            if (!_uiComponents.TryGetValue(rule.TransformPath, out var pathRules))
            {
                pathRules = new List<UiRule>();
                _uiComponents[rule.TransformPath] = pathRules;
            }
            pathRules.Add(
                new UiRule(
                    string.IsNullOrWhiteSpace(rule.SourceSprite) ? null : rule.SourceSprite,
                    fullPath
                )
            );
            if (string.IsNullOrWhiteSpace(rule.SourceSprite))
                _hasUnscopedUiRules = true;
            else
                _uiSourceSprites.Add(rule.SourceSprite);
        }
    }

    private bool TryResolveImagePath(
        string relativeFile,
        out string fullPath,
        out string error
    )
    {
        fullPath = null;
        error = null;
        if (string.IsNullOrWhiteSpace(relativeFile))
        {
            error = "file is empty";
            return false;
        }
        if (
            Path.IsPathRooted(relativeFile)
            || Path.IsPathFullyQualified(relativeFile)
            || relativeFile.Contains(':')
            || relativeFile.IndexOf('\0') >= 0
        )
        {
            error = "file must be a local relative path";
            return false;
        }
        string extension = Path.GetExtension(relativeFile);
        if (
            !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
        )
        {
            error = "only PNG, JPG, and JPEG files are supported";
            return false;
        }

        try
        {
            string candidate = Path.GetFullPath(Path.Combine(_replacementRoot, relativeFile));
            string rootPrefix = _replacementRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "file escapes the replacements directory";
                return false;
            }
            fullPath = candidate;
            return true;
        }
        catch (Exception e)
        {
            error = $"invalid file path ({e.Message})";
            return false;
        }
    }

    private bool TryMatchSpriteName(Sprite original, out string file)
    {
        file = null;
        if (original == null || string.IsNullOrEmpty(original.name))
            return false;
        return _spriteNames.TryGetValue(original.name, out file);
    }

    private static bool TryMatchUiRule(
        List<UiRule> rules,
        Sprite original,
        out string file
    )
    {
        file = null;
        string sourceName = original?.name;
        foreach (var rule in rules)
        {
            if (
                rule.SourceSprite != null
                && string.Equals(rule.SourceSprite, sourceName, StringComparison.Ordinal)
            )
            {
                file = rule.File;
                return true;
            }
        }
        foreach (var rule in rules)
        {
            if (rule.SourceSprite == null)
            {
                file = rule.File;
                return true;
            }
        }
        return false;
    }

    private Sprite GetOrCreateSprite(string file, Sprite original)
    {
        if (original == null)
            return original;

        SpriteCacheKey cacheKey = default;
        bool hasCacheKey = false;
        try
        {
            Texture2D texture = GetOrLoadTexture(file, original);
            if (texture == null)
                return original;

            Rect sourceRect = original.rect;
            Vector2 pivot =
                sourceRect.width <= 0f || sourceRect.height <= 0f
                    ? new Vector2(0.5f, 0.5f)
                    : new Vector2(
                        original.pivot.x / sourceRect.width,
                        original.pivot.y / sourceRect.height
                    );
            float pixelsPerUnit = original.pixelsPerUnit;
            Vector4 border = original.border;
            Texture2D spriteTexture = texture;
            int sourceWidth = Mathf.RoundToInt(sourceRect.width);
            int sourceHeight = Mathf.RoundToInt(sourceRect.height);

            if (sourceWidth != texture.width || sourceHeight != texture.height)
            {
                if (TryGetLogicalCanvasLayout(texture, original, sourceRect, out var layout))
                {
                    spriteTexture = GetOrCreateLogicalCanvas(
                        file,
                        texture,
                        layout,
                        out bool canvasCreated
                    );
                    if (spriteTexture == null)
                        return original;
                    if (canvasCreated)
                    {
                        Logger.Info(
                            $"Replacement image canvas restored for Sprite '{original.name}': "
                                + $"content={texture.width}x{texture.height}, "
                                + $"canvas={spriteTexture.width}x{spriteTexture.height}, "
                                + $"offset={layout.OffsetX},{layout.OffsetY} "
                                + $"({(layout.UsedSpriteOffset ? "SpriteAtlas metadata" : "centered fallback")})."
                        );
                    }
                }
                else if (
                    _dimensionWarnings.Add(
                        new DimensionWarningKey(
                            file,
                            sourceWidth,
                            sourceHeight,
                            texture.width,
                            texture.height
                        )
                    )
                )
                {
                    Logger.Warn(
                        $"Replacement image size differs for Sprite '{original.name}': "
                            + $"source={sourceWidth}x{sourceHeight}, "
                            + $"replacement={texture.width}x{texture.height}; "
                            + "the replacement could not be padded automatically."
                    );
                }
            }

            border = FitBorder(border, spriteTexture.width, spriteTexture.height);
            cacheKey = new SpriteCacheKey(
                file,
                spriteTexture.GetInstanceID(),
                original.name,
                pivot.x,
                pivot.y,
                pixelsPerUnit,
                border.x,
                border.y,
                border.z,
                border.w
            );
            hasCacheKey = true;
            if (_spriteCache.TryGetValue(cacheKey, out var cachedSprite))
                return cachedSprite;
            if (_failedSprites.Contains(cacheKey))
                return original;

            var replacement = Sprite.Create(
                spriteTexture,
                new Rect(0f, 0f, spriteTexture.width, spriteTexture.height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border
            );
            replacement.name = original.name;
            replacement.hideFlags = HideFlags.DontUnloadUnusedAsset;

            _spriteCache[cacheKey] = replacement;
            int replacementId = replacement.GetInstanceID();
            _replacementSpriteIds.Add(replacementId);
            _replacementSpriteFiles[replacementId] = file;
            return replacement;
        }
        catch (Exception e)
        {
            if (hasCacheKey)
                _failedSprites.Add(cacheKey);
            Logger.Error($"Failed to create replacement Sprite '{Path.GetFileName(file)}': {e}");
            return original;
        }
    }

    private Texture2D GetOrLoadTexture(string file, Sprite original)
    {
        if (_textureCache.TryGetValue(file, out var cachedTexture))
            return cachedTexture;
        if (_failedFiles.Contains(file))
            return null;

        Texture2D texture = null;
        try
        {
            var imageFile = new FileInfo(file);
            if (!imageFile.Exists)
                throw new FileNotFoundException("replacement image was not found", file);
            if (imageFile.Length <= 0 || imageFile.Length > MaxImageBytes)
                throw new InvalidDataException(
                    $"image size must be between 1 byte and {MaxImageBytes / 1024 / 1024} MiB"
                );

            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.LongLength <= 0 || bytes.LongLength > MaxImageBytes)
                throw new InvalidDataException(
                    $"image size must be between 1 byte and {MaxImageBytes / 1024 / 1024} MiB"
                );
            ValidateImageHeader(bytes, Path.GetExtension(file));
            var imageData = new Il2CppStructArray<byte>(bytes);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"AbyssMod:{Path.GetFileName(file)}",
                hideFlags = HideFlags.DontUnloadUnusedAsset,
                wrapMode = TextureWrapMode.Clamp,
            };
            if (original != null && original.texture != null)
            {
                texture.filterMode = original.texture.filterMode;
                texture.anisoLevel = original.texture.anisoLevel;
            }
            if (!ImageConversion.LoadImage(texture, imageData, true))
                throw new InvalidDataException("Unity could not decode the image");
            ValidateImageDimensions(
                (uint)texture.width,
                (uint)texture.height,
                "decoded image"
            );

            _textureCache[file] = texture;
            Logger.Info($"Replacement image loaded: {Path.GetRelativePath(_replacementRoot, file)}");
            return texture;
        }
        catch (Exception e)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
            _failedFiles.Add(file);
            Logger.Error($"Failed to load replacement image '{Path.GetFileName(file)}': {e.Message}");
            return null;
        }
    }

    private static void ValidateImageHeader(byte[] bytes, string extension)
    {
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
        {
            ValidatePngHeader(bytes);
            return;
        }
        ValidateJpegHeader(bytes);
    }

    private static void ValidatePngHeader(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = new byte[]
        {
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A,
        };
        if (
            bytes.Length < 24
            || !bytes.AsSpan(0, signature.Length).SequenceEqual(signature)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13
            || bytes[12] != (byte)'I'
            || bytes[13] != (byte)'H'
            || bytes[14] != (byte)'D'
            || bytes[15] != (byte)'R'
        )
            throw new InvalidDataException("file does not contain a valid PNG header");

        uint width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        uint height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        ValidateImageDimensions(width, height, "PNG");
    }

    private static void ValidateJpegHeader(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            throw new InvalidDataException("file does not contain a valid JPEG header");

        int offset = 2;
        while (offset < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
                throw new InvalidDataException("JPEG marker is missing its 0xFF prefix");
            while (offset < bytes.Length && bytes[offset] == 0xFF)
                offset++;
            if (offset >= bytes.Length)
                break;

            byte marker = bytes[offset++];
            if (marker == 0xD9 || marker == 0xDA)
                break;
            if (marker == 0x00 || marker == 0xD8)
                throw new InvalidDataException("JPEG contains an invalid marker");
            if (marker == 0x01 || marker >= 0xD0 && marker <= 0xD7)
                continue;
            if (offset + 2 > bytes.Length)
                break;

            int segmentLength = bytes[offset] << 8 | bytes[offset + 1];
            if (segmentLength < 2 || offset + segmentLength > bytes.Length)
                throw new InvalidDataException("JPEG contains an invalid segment length");

            bool isStartOfFrame =
                marker >= 0xC0
                && marker <= 0xCF
                && marker != 0xC4
                && marker != 0xC8
                && marker != 0xCC;
            if (isStartOfFrame)
            {
                if (segmentLength < 11)
                    throw new InvalidDataException("JPEG frame header is incomplete");
                int componentCount = bytes[offset + 7];
                if (
                    componentCount <= 0
                    || segmentLength != 8 + 3 * componentCount
                )
                    throw new InvalidDataException("JPEG frame header has an invalid length");
                uint height = (uint)(bytes[offset + 3] << 8 | bytes[offset + 4]);
                uint width = (uint)(bytes[offset + 5] << 8 | bytes[offset + 6]);
                ValidateImageDimensions(width, height, "JPEG");
                return;
            }

            offset += segmentLength;
        }

        throw new InvalidDataException("JPEG dimensions could not be read");
    }

    private static void ValidateImageDimensions(uint width, uint height, string format)
    {
        if (
            width == 0
            || height == 0
            || width > MaxImageDimension
            || height > MaxImageDimension
            || (long)width * height > MaxImagePixels
        )
        {
            throw new InvalidDataException(
                $"{format} dimensions {width}x{height} exceed the supported limit"
            );
        }
    }

    private static Vector4 FitBorder(Vector4 border, int width, int height)
    {
        float horizontal = border.x + border.z;
        if (horizontal > width && horizontal > 0f)
        {
            float scale = width / horizontal;
            border.x *= scale;
            border.z *= scale;
        }
        float vertical = border.y + border.w;
        if (vertical > height && vertical > 0f)
        {
            float scale = height / vertical;
            border.y *= scale;
            border.w *= scale;
        }
        return border;
    }

    private static bool TryGetLogicalCanvasLayout(
        Texture2D source,
        Sprite original,
        Rect sourceRect,
        out LogicalCanvasLayout layout
    )
    {
        layout = default;

        int targetWidth = Mathf.RoundToInt(sourceRect.width);
        int targetHeight = Mathf.RoundToInt(sourceRect.height);
        if (
            targetWidth <= 0
            || targetHeight <= 0
            || source.width > targetWidth
            || source.height > targetHeight
            || source.width == targetWidth && source.height == targetHeight
        )
            return false;

        int offsetX = 0;
        int offsetY = 0;
        bool usedSpriteOffset = false;

        // SpriteAtlas can trim transparent pixels. When the replacement is the same
        // size as the packed rectangle, textureRectOffset puts it back at the exact
        // position inside the original logical Sprite rectangle.
        try
        {
            Rect packedRect = original.textureRect;
            Vector2 packedOffset = original.textureRectOffset;
            int packedWidth = Mathf.RoundToInt(packedRect.width);
            int packedHeight = Mathf.RoundToInt(packedRect.height);
            int candidateX = Mathf.RoundToInt(packedOffset.x);
            int candidateY = Mathf.RoundToInt(packedOffset.y);
            if (
                source.width == packedWidth
                && source.height == packedHeight
                && FitsCanvas(
                    candidateX,
                    candidateY,
                    source.width,
                    source.height,
                    targetWidth,
                    targetHeight
                )
            )
            {
                offsetX = candidateX;
                offsetY = candidateY;
                usedSpriteOffset = true;
            }
        }
        catch
        {
            // Some tightly packed Sprite implementations do not expose textureRect.
        }

        if (!usedSpriteOffset)
        {
            offsetX = (targetWidth - source.width) / 2;
            offsetY = (targetHeight - source.height) / 2;
        }

        if (
            !FitsCanvas(
                offsetX,
                offsetY,
                source.width,
                source.height,
                targetWidth,
                targetHeight
            )
        )
            return false;

        layout = new LogicalCanvasLayout(
            targetWidth,
            targetHeight,
            offsetX,
            offsetY,
            usedSpriteOffset
        );
        return true;
    }

    private Texture2D GetOrCreateLogicalCanvas(
        string file,
        Texture2D source,
        LogicalCanvasLayout layout,
        out bool created
    )
    {
        created = false;
        var key = new LogicalCanvasKey(
            file,
            layout.Width,
            layout.Height,
            layout.OffsetX,
            layout.OffsetY
        );
        if (_logicalCanvasCache.TryGetValue(key, out var cached))
            return cached;
        if (_failedCanvases.Contains(key))
            return null;

        try
        {
            Texture2D canvas = CreateLogicalCanvas(source, layout);
            _logicalCanvasCache[key] = canvas;
            created = true;
            return canvas;
        }
        catch (Exception e)
        {
            _failedCanvases.Add(key);
            Logger.Error(
                $"Failed to restore replacement image canvas '{Path.GetFileName(file)}': {e.Message}"
            );
            return null;
        }
    }

    private static Texture2D CreateLogicalCanvas(
        Texture2D source,
        LogicalCanvasLayout layout
    )
    {
        RenderTexture converted = null;
        RenderTexture temporary = null;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D result = null;
        try
        {
            // LoadImage commonly decodes PNG and JPEG to different GPU formats.
            // Blit both through ARGB32 first so the following region copy is portable.
            converted = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default
            );
            Graphics.Blit(source, converted);

            temporary = RenderTexture.GetTemporary(
                layout.Width,
                layout.Height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default
            );
            RenderTexture.active = temporary;
            GL.Clear(true, true, Color.clear);

            // Copy only the visible, trimmed rectangle after explicitly clearing the
            // full logical canvas. A new texture is not guaranteed to be transparent.
            Graphics.CopyTexture(
                converted,
                0,
                0,
                0,
                0,
                source.width,
                source.height,
                temporary,
                0,
                0,
                layout.OffsetX,
                layout.OffsetY
            );

            result = new Texture2D(layout.Width, layout.Height, TextureFormat.RGBA32, false)
            {
                name = $"{source.name}:logical-canvas",
                hideFlags = HideFlags.DontUnloadUnusedAsset,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel,
            };
            result.ReadPixels(new Rect(0f, 0f, layout.Width, layout.Height), 0, 0, false);
            result.Apply(false, true);

            Texture2D canvas = result;
            result = null;
            return canvas;
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (converted != null)
                RenderTexture.ReleaseTemporary(converted);
            if (temporary != null)
                RenderTexture.ReleaseTemporary(temporary);
            if (result != null)
                UnityEngine.Object.Destroy(result);
        }
    }

    private static bool FitsCanvas(
        int x,
        int y,
        int width,
        int height,
        int canvasWidth,
        int canvasHeight
    ) =>
        x >= 0
        && y >= 0
        && width > 0
        && height > 0
        && x + width <= canvasWidth
        && y + height <= canvasHeight;

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
            return null;

        var parts = new Stack<string>();
        for (var current = transform; current != null; current = current.parent)
            parts.Push(current.name);
        return string.Join("/", parts);
    }

    private int CountUiRules()
    {
        int count = 0;
        foreach (var rules in _uiComponents.Values)
            count += rules.Count;
        return count;
    }

    private void ClearRules()
    {
        _novelBackgrounds.Clear();
        _spriteNames.Clear();
        _uiComponents.Clear();
        _uiSourceSprites.Clear();
        _hasUnscopedUiRules = false;
    }

    private readonly record struct LogicalCanvasLayout(
        int Width,
        int Height,
        int OffsetX,
        int OffsetY,
        bool UsedSpriteOffset
    );

    private readonly record struct LogicalCanvasKey(
        string File,
        int Width,
        int Height,
        int OffsetX,
        int OffsetY
    );

    private readonly record struct DimensionWarningKey(
        string File,
        int SourceWidth,
        int SourceHeight,
        int ReplacementWidth,
        int ReplacementHeight
    );

    private readonly record struct SpriteCacheKey(
        string File,
        int TextureId,
        string Name,
        float PivotX,
        float PivotY,
        float PixelsPerUnit,
        float BorderLeft,
        float BorderBottom,
        float BorderRight,
        float BorderTop
    );

    private sealed class UiRule
    {
        public UiRule(string sourceSprite, string file)
        {
            SourceSprite = sourceSprite;
            File = file;
        }

        public string SourceSprite { get; }
        public string File { get; }
    }
}
