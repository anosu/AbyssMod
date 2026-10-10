using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using static AbyssMod.Services.ImageReplacementLimits;

namespace AbyssMod.Services;

/// <summary>按替换规则加载纹理、恢复逻辑画布并管理 Sprite 的生命周期。</summary>
internal sealed class ImageReplacementManager : IDisposable
{
    private readonly string _replacementRoot;
    private ImageReplacementRules _rules;
    private readonly Dictionary<string, Texture2D> _textureCache = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<LogicalCanvasKey, Texture2D> _logicalCanvasCache = new();
    private readonly Dictionary<SpriteCacheKey, Sprite> _spriteCache = new();
    private readonly Dictionary<int, string> _replacementSpriteFiles = new();
    private readonly HashSet<string> _failedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<LogicalCanvasKey> _failedCanvases = new();
    private readonly HashSet<SpriteCacheKey> _failedSprites = new();
    private readonly HashSet<DimensionWarningKey> _dimensionWarnings = new();
    private bool _disposed;

    public ImageReplacementManager(string replacementRoot) =>
        _replacementRoot = Path.GetFullPath(replacementRoot);

    public bool Enabled => _rules?.Count > 0;

    public void Initialize()
    {
        string manifestPath = Path.Combine(_replacementRoot, ImageReplacementPaths.ManifestFile);
        if (!File.Exists(manifestPath))
        {
            Logger.Info($"Image replacement manifest not found: {manifestPath}");
            return;
        }
        try
        {
            if (new FileInfo(manifestPath).Length > MaxManifestBytes)
                throw new InvalidDataException($"manifest exceeds {MaxManifestBytes / 1024} KiB");
            var manifest =
                JsonSerializer.Deserialize<ImageReplacementManifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                    }
                ) ?? throw new InvalidDataException("manifest root is null");
            _rules = new ImageReplacementRules(_replacementRoot, manifest);
            Logger.Info($"Image replacement manifest loaded: {_rules.Count} rules");
        }
        catch (Exception e)
        {
            _rules = null;
            Logger.Error($"Image replacement manifest load failed: {e.Message}");
        }
    }

    public Sprite ReplaceNovelBackground(string id, Sprite original)
    {
        if (!CanReplace(original))
            return original;
        string file = _rules?.MatchBackground(id, original.name);
        return file != null ? GetOrCreateSprite(file, original) : original;
    }

    public Sprite ReplaceUiImage(Image image, Sprite original)
    {
        if (_disposed || original == null)
            return original;
        bool isReplacement = _replacementSpriteFiles.TryGetValue(
            original.GetInstanceID(),
            out string originalFile
        );
        if (_rules?.MayMatchUi(original.name) == true)
        {
            string file = _rules.MatchUi(TransformPath.Build(image?.transform), original.name);
            if (file != null)
            {
                if (
                    isReplacement
                    && string.Equals(originalFile, file, StringComparison.OrdinalIgnoreCase)
                )
                    return original;
                return GetOrCreateSprite(file, original);
            }
        }
        return isReplacement ? original : ReplaceBySpriteName(original);
    }

    public Sprite ReplaceBySpriteName(Sprite original)
    {
        if (!CanReplace(original))
            return original;
        string file = _rules?.MatchSpriteName(original.name);
        return file != null ? GetOrCreateSprite(file, original) : original;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var sprite in _spriteCache.Values)
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
        foreach (var texture in _textureCache.Values)
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        foreach (var texture in _logicalCanvasCache.Values)
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        _spriteCache.Clear();
        _textureCache.Clear();
        _logicalCanvasCache.Clear();
        _replacementSpriteFiles.Clear();
        _failedFiles.Clear();
        _failedCanvases.Clear();
        _failedSprites.Clear();
        _dimensionWarnings.Clear();
        _rules = null;
    }

    private bool CanReplace(Sprite original) =>
        !_disposed
        && original != null
        && !_replacementSpriteFiles.ContainsKey(original.GetInstanceID());

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
                        Logger.Info(
                            $"Replacement image canvas restored for Sprite '{original.name}': "
                                + $"content={texture.width}x{texture.height}, canvas={spriteTexture.width}x{spriteTexture.height}, "
                                + $"offset={layout.OffsetX},{layout.OffsetY} "
                                + $"({(layout.UsedSpriteOffset ? "SpriteAtlas metadata" : "centered fallback")})."
                        );
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
                            + $"source={sourceWidth}x{sourceHeight}, replacement={texture.width}x{texture.height}; "
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
            _replacementSpriteFiles[replacement.GetInstanceID()] = file;
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
            ImageFileValidation.Validate(bytes, Path.GetExtension(file));
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
            ImageFileValidation.ValidateDimensions(
                (uint)texture.width,
                (uint)texture.height,
                "decoded image"
            );
            _textureCache[file] = texture;
            Logger.Info(
                $"Replacement image loaded: {Path.GetRelativePath(_replacementRoot, file)}"
            );
            return texture;
        }
        catch (Exception e)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
            _failedFiles.Add(file);
            Logger.Error(
                $"Failed to load replacement image '{Path.GetFileName(file)}': {e.Message}"
            );
            return null;
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
        out ImageCanvasLayout layout
    )
    {
        (int Width, int Height, int X, int Y)? packedSprite = null;
        try
        {
            // SpriteAtlas 的裁剪偏移优先于居中；部分紧密打包 Sprite 不提供这些属性。
            Rect packedRect = original.textureRect;
            Vector2 packedOffset = original.textureRectOffset;
            packedSprite = (
                Mathf.RoundToInt(packedRect.width),
                Mathf.RoundToInt(packedRect.height),
                Mathf.RoundToInt(packedOffset.x),
                Mathf.RoundToInt(packedOffset.y)
            );
        }
        catch { }
        return ImageCanvasLayout.TryCreate(
            source.width,
            source.height,
            Mathf.RoundToInt(sourceRect.width),
            Mathf.RoundToInt(sourceRect.height),
            packedSprite,
            out layout
        );
    }

    private Texture2D GetOrCreateLogicalCanvas(
        string file,
        Texture2D source,
        ImageCanvasLayout layout,
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

    private static Texture2D CreateLogicalCanvas(Texture2D source, ImageCanvasLayout layout)
    {
        RenderTexture converted = null;
        RenderTexture temporary = null;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D result = null;
        try
        {
            // PNG/JPEG 先转换到同一 GPU 格式，再复制裁剪区域。
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
}
