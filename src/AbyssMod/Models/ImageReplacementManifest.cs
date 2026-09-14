using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AbyssMod;

/// <summary>本地图片替换清单。</summary>
public sealed class ImageReplacementManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("novelBackgrounds")]
    public Dictionary<string, string> NovelBackgrounds { get; set; } = new();

    [JsonPropertyName("spriteNames")]
    public Dictionary<string, string> SpriteNames { get; set; } = new();

    [JsonPropertyName("uiComponents")]
    public List<UiImageReplacement> UiComponents { get; set; } = new();
}

public sealed class UiImageReplacement
{
    [JsonPropertyName("transformPath")]
    public string TransformPath { get; set; }

    [JsonPropertyName("sourceSprite")]
    public string SourceSprite { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; }
}
