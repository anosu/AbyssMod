using System.Collections.Generic;
using AbyssMod.Services;
using Xunit;

namespace AbyssMod.Tests;

public class UiTextIndexTests
{
    [Fact]
    public void ExactPathWinsOverWildcardAndDoesNotLeakToOtherScreens()
    {
        var index = new UiTextIndex(new()
        {
            ["Root/*/Label"] = new() { ["Start"] = "开始" },
            ["Root/Home/Label"] = new() { ["Start"] = "进入" },
        });

        Assert.True(index.TryTranslate("Root/Home/Label", "Start", out var exact));
        Assert.Equal("进入", exact);
        Assert.True(index.TryTranslate("Root/Battle/Label", "Start", out var wildcard));
        Assert.Equal("开始", wildcard);
        Assert.False(index.TryTranslate("Other/Home/Label", "Start", out _));
        Assert.False(index.TryTranslate("Root/Home/Label", "Unknown", out _));
    }

    [Fact]
    public void WildcardStaysWithinOnePathSegmentAndMostSpecificRuleWins()
    {
        var index = new UiTextIndex(new()
        {
            ["Root/*/Label"] = new() { ["Start"] = "开始" },
            ["Root/Battle*/Label"] = new() { ["Start"] = "战斗" },
        });

        Assert.True(index.TryTranslate("Root/Battle(Clone)/Label", "Start", out var result));
        Assert.Equal("战斗", result);
        Assert.False(index.TryTranslate("Root/Battle/Child/Label", "Start", out _));
    }

    [Theory]
    [InlineData("HP: 10 / 20", "生命：20 中剩余 10")]
    [InlineData("HP: <b>10</b> / 20", "生命：20 中剩余 <b>10</b>")]
    public void PlaceholdersPreserveCapturedTextAndCanBeReordered(string source, string expected)
    {
        var index = new UiTextIndex(new()
        {
            ["Root/Stats"] = new() { ["HP: {0} / {1}"] = "生命：{1} 中剩余 {0}" },
        });

        Assert.True(index.TryTranslate("Root/Stats", source, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RepeatedPlaceholderMustMatchTheSameValue()
    {
        var index = new UiTextIndex(new()
        {
            ["Root/Stats"] = new() { ["{0} + {0}"] = "两倍 {0}" },
        });

        Assert.True(index.TryTranslate("Root/Stats", "3 + 3", out var result));
        Assert.Equal("两倍 3", result);
        Assert.False(index.TryTranslate("Root/Stats", "3 + 4", out _));
    }

    [Fact]
    public void ExactTextWinsOverTemplateAndEmptyTranslationsAreIgnored()
    {
        var index = new UiTextIndex(new()
        {
            ["Root/Stats"] = new()
            {
                ["HP: {0}"] = "生命：{0}",
                ["HP: 0"] = "已阵亡",
                ["Untranslated"] = "",
            },
        });

        Assert.True(index.TryTranslate("Root/Stats", "HP: 0", out var result));
        Assert.Equal("已阵亡", result);
        Assert.False(index.TryTranslate("Root/Stats", "Untranslated", out _));
        Assert.False(index.TryTranslate(null, "HP: 0", out _));
        Assert.False(index.TryTranslate("Root/Stats", null, out _));
    }
}
