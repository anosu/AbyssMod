using System;
using System.IO;
using AbyssMod;
using AbyssMod.Services;
using Xunit;

namespace CacheTests;

public sealed class ImageRulesTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AbyssMod-rules");

    [Fact]
    public void BackgroundAndScopedUiRulesTakePriorityOverFallbacks()
    {
        var rules = new ImageReplacementRules(
            _root,
            new ImageReplacementManifest
            {
                NovelBackgrounds = new() { ["scene"] = "background.png" },
                SpriteNames = new() { ["sprite"] = "sprite.png" },
                UiComponents = new()
                {
                    new() { TransformPath = "Root/Image", File = "fallback.png" },
                    new()
                    {
                        TransformPath = "Root/Image",
                        SourceSprite = "sprite",
                        File = "specific.png",
                    },
                },
            }
        );
        Assert.Equal(
            Path.Combine(_root, "background.png"),
            rules.MatchBackground("scene", "sprite")
        );
        Assert.Equal(Path.Combine(_root, "sprite.png"), rules.MatchBackground("other", "sprite"));
        Assert.Equal(Path.Combine(_root, "specific.png"), rules.MatchUi("Root/Image", "sprite"));
        Assert.Equal(Path.Combine(_root, "fallback.png"), rules.MatchUi("Root/Image", "other"));
        Assert.Null(rules.MatchUi("Other/Image", "sprite"));
        Assert.Null(rules.MatchSpriteName("unknown"));
    }

    [Fact]
    public void InvalidRulesAreSkippedWithoutLosingValidLocalRelativePaths()
    {
        var rules = new ImageReplacementRules(
            _root,
            new ImageReplacementManifest
            {
                SpriteNames = new()
                {
                    ["valid"] = "./images/a.PNG",
                    ["escape"] = "../outside.png",
                    ["format"] = "script.exe",
                    [""] = "empty.png",
                },
                UiComponents = new()
                {
                    new()
                    {
                        TransformPath = "Root/Image",
                        SourceSprite = "valid",
                        File = "ui.jpg",
                    },
                },
            }
        );
        Assert.Equal(2, rules.Count);
        Assert.Equal(Path.Combine(_root, "images", "a.PNG"), rules.MatchSpriteName("valid"));
        Assert.Null(rules.MatchSpriteName("escape"));
        Assert.Null(rules.MatchSpriteName("format"));
        Assert.True(rules.MayMatchUi("valid"));
        Assert.False(rules.MayMatchUi("other"));
        Assert.Throws<InvalidDataException>(() =>
            new ImageReplacementRules(_root, new ImageReplacementManifest { Version = 999 })
        );
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("images/../../outside.jpg")]
    [InlineData("/absolute.png")]
    [InlineData("C:/absolute.png")]
    [InlineData("file.png:stream")]
    [InlineData("file.txt")]
    [InlineData("")]
    public void ImagePathsRejectEscapesAndUnsupportedFiles(string path) =>
        Assert.Throws<InvalidDataException>(() => ImageReplacementPaths.ResolveImage(_root, path));

    [Fact]
    public void RemotePathsNormalizeSeparatorsButRejectAmbiguousSegments()
    {
        Assert.Equal(
            "images/a.png",
            ImageReplacementPaths.NormalizeRemoteFile("images\\a.png", false)
        );
        Assert.Equal(
            "manifest.json",
            ImageReplacementPaths.NormalizeRemoteFile("MANIFEST.JSON", true)
        );
        Assert.Throws<InvalidDataException>(() =>
            ImageReplacementPaths.NormalizeRemoteFile("./a.png", false)
        );
        Assert.Throws<InvalidDataException>(() =>
            ImageReplacementPaths.NormalizeRemoteFile("a//b.png", false)
        );
        Assert.Throws<InvalidDataException>(() =>
            ImageReplacementPaths.NormalizeRemoteFile("manifest.json", false)
        );
    }
}
