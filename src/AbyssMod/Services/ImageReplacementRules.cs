#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AbyssMod.Services;

/// <summary>解析并索引图片替换规则；不读取文件，也不访问 Unity 对象。</summary>
internal sealed class ImageReplacementRules
{
    private readonly string _root;
    private readonly Dictionary<string, string> _backgrounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _spriteNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<UiRule>> _uiRules = new(StringComparer.Ordinal);
    private readonly HashSet<string> _uiSources = new(StringComparer.Ordinal);
    private bool _hasUnscopedUiRules;

    internal ImageReplacementRules(string root, ImageReplacementManifest manifest)
    {
        if (manifest.Version != ImageReplacementLimits.SupportedManifestVersion)
            throw new InvalidDataException($"unsupported manifest version {manifest.Version}");
        _root = Path.GetFullPath(root);
        AddMappings(manifest.NovelBackgrounds, _backgrounds, "novelBackgrounds");
        AddMappings(manifest.SpriteNames, _spriteNames, "spriteNames");
        AddUiRules(manifest.UiComponents);
    }

    internal int Count =>
        _backgrounds.Count + _spriteNames.Count + _uiRules.Values.Sum(rules => rules.Count);

    internal string? MatchBackground(string? id, string? spriteName) =>
        id != null && _backgrounds.TryGetValue(id, out var file)
            ? file
            : MatchSpriteName(spriteName);

    internal string? MatchSpriteName(string? name) =>
        name != null && _spriteNames.TryGetValue(name, out var file) ? file : null;

    internal bool MayMatchUi(string? sourceName) =>
        _uiRules.Count > 0
        && (_hasUnscopedUiRules || sourceName != null && _uiSources.Contains(sourceName));

    internal string? MatchUi(string? path, string? sourceName)
    {
        if (path == null || !_uiRules.TryGetValue(path, out var rules))
            return null;
        foreach (var rule in rules)
            if (rule.SourceName != null && rule.SourceName == sourceName)
                return rule.File;
        foreach (var rule in rules)
            if (rule.SourceName == null)
                return rule.File;
        return null;
    }

    private void AddMappings(
        Dictionary<string, string>? source,
        Dictionary<string, string> target,
        string section
    )
    {
        if (source == null)
            return;
        foreach (var (key, file) in source)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Logger.Warn($"Image replacement [{section}] skipped an empty key.");
                continue;
            }
            string? path = ResolveFile(file, section, key);
            if (path != null)
                target[key] = path;
        }
    }

    private void AddUiRules(List<UiImageReplacement>? source)
    {
        if (source == null)
            return;
        foreach (var rule in source)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.TransformPath))
            {
                Logger.Warn("Image replacement [uiComponents] skipped an empty transformPath.");
                continue;
            }
            string? file = ResolveFile(rule.File, "uiComponents", rule.TransformPath);
            if (file == null)
                continue;
            if (!_uiRules.TryGetValue(rule.TransformPath, out var rules))
                _uiRules[rule.TransformPath] = rules = new List<UiRule>();
            string? sourceName = string.IsNullOrWhiteSpace(rule.SourceSprite)
                ? null
                : rule.SourceSprite;
            rules.Add(new UiRule(sourceName, file));
            if (sourceName == null)
                _hasUnscopedUiRules = true;
            else
                _uiSources.Add(sourceName);
        }
    }

    private string? ResolveFile(string file, string section, string key)
    {
        try
        {
            return ImageReplacementPaths.ResolveImage(_root, file);
        }
        catch (Exception e)
        {
            Logger.Warn($"Image replacement [{section}] '{key}' skipped: {e.Message}");
            return null;
        }
    }

    private readonly record struct UiRule(string? SourceName, string File);
}
