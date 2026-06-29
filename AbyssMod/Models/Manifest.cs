using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbyssMod;

/// <summary>
/// 翻译清单数据结构，对应远程 manifest.json 格式。
///
/// 除 hash/novels 外的所有键（names/titles/descriptions/.../任意新增类型）
/// 统一收纳进 Files，新增翻译类型无需修改本类。
/// </summary>
public class Manifest
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    /// <summary>
    /// manifest 中所有未显式声明的键（即每个翻译文件的哈希）。
    /// 键为类型名（如 "names"），值为规范化哈希字符串。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Extra { get; set; } = new();

    [JsonPropertyName("novels")]
    public Dictionary<string, string> Novels { get; set; }

    /// <summary>
    /// 获取指定翻译类型的清单哈希。不存在时返回 null。
    /// Extra 反序列化时，纯字符串值会被解析为 JsonElement。
    /// </summary>
    public string GetFileHash(string type)
    {
        if (Extra == null || !Extra.TryGetValue(type, out var value) || value == null)
            return null;

        return value switch
        {
            string s => s,
            JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : null,
            _ => null,
        };
    }
}
