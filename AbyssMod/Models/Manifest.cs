using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbyssMod;

/// <summary>
/// 翻译清单，对应远程 manifest.json。
/// Extra 收纳所有未显式声明的文件哈希，新增翻译类型无需改本类。
/// </summary>
public class Manifest
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> Extra { get; set; } = new();

    [JsonPropertyName("novels")]
    public Dictionary<string, string> Novels { get; set; }

    /// <summary>获取指定类型的清单哈希，不存在时返回 null。</summary>
    public string GetFileHash(string type)
    {
        if (Extra == null || !Extra.TryGetValue(type, out var value) || value == null)
            return null;

        return value is JsonElement je
            ? je.ValueKind == JsonValueKind.String
                ? je.GetString()
                : null
            : value as string;
    }
}
