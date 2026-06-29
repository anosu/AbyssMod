using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Il2CppInterop.Runtime;

namespace AbyssMod.Services;

/// <summary>
/// 一个字段在运行期需要的信息：缓存后的字段指针与字节偏移，以及字段级后处理配置。
/// 字段指针/偏移只依赖表类，与具体 row 无关，故在加载期一次性解析并缓存。
/// </summary>
public class FieldEntry
{
    public string Name { get; init; }
    public IntPtr FieldPtr { get; init; }
    public int Offset { get; init; }
    public bool Seal { get; init; }
}

/// <summary>
/// 一张 MasterData 表的翻译规则。
/// </summary>
public class TableMapping
{
    public string TableKey { get; init; }
    public string TranslationKey { get; init; }
    public string ClassName { get; init; }
    public List<FieldEntry> Fields { get; } = new();
}

/// <summary>
/// 解析嵌入资源 master_mapping.json，构建「IL2CPP 类名 → TableMapping」索引，
/// 并在加载期解析每个字段的指针与偏移（等价于编译期属性 getter/setter 的内部逻辑）。
///
/// <para>JSON 中 tables 的键直接使用 MasterData/static 的 snake_case 表名。</para>
/// <para>失败隔离：解析失败或资源缺失 → 返回空映射并告警，不抛异常，游戏继续运行（仅不翻译）。</para>
/// <para>字段预检：无法解析的字段（typo / 类型在游戏中不存在）会被剔除并告警，运行期不再触碰。</para>
/// </summary>
public static class MasterMapping
{
    private const string ResourceName = "AbyssMod.config.master.json";

    /// <summary>IL2CPP 类名 → 该表的字段映射。加载后只读。</summary>
    public static Dictionary<string, TableMapping> Tables { get; private set; } = new();

    /// <summary>所有需加载的翻译资源类型列表。由 Load() 填充。</summary>
    public static IReadOnlyList<string> ContentTypes { get; private set; } = Array.Empty<string>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>加载并预解析映射。幂等；失败时 Tables 置空并告警。</summary>
    public static void Load()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                Logger.Error($"[MasterMapping] embedded resource not found: {ResourceName}");
                Tables = new();
                return;
            }

            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("tables", out var tablesEl))
            {
                Logger.Error("[MasterMapping] missing 'tables' property");
                Tables = new();
                ContentTypes = ReadFlatTypes(doc.RootElement);
                return;
            }

            var result = new Dictionary<string, TableMapping>(StringComparer.Ordinal);
            var contentTypes = new List<string>();
            var contentTypeSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tableProp in tablesEl.EnumerateObject())
            {
                var table = BuildTable(tableProp.Name, tableProp.Value);
                if (table.Fields.Count == 0)
                    continue;

                result[table.ClassName] = table;
                AddContentType(contentTypes, contentTypeSet, table.TranslationKey);
            }

            foreach (var type in ReadFlatTypes(doc.RootElement))
                AddContentType(contentTypes, contentTypeSet, type);

            ContentTypes = contentTypes;
            Tables = result;
            Logger.Info(
                $"[MasterMapping] loaded {result.Count} tables, {CountFields(result)} fields"
            );
            Logger.Info($"[MasterMapping] registered {contentTypes.Count} content types");
        }
        catch (Exception e)
        {
            Logger.Error($"[MasterMapping] failed to load: {e}");
            Tables = new();
        }
    }

    private static TableMapping BuildTable(string tableName, JsonElement tableEl)
    {
        var className = ToClassName(tableName);
        var translationKey = ReadTranslationKey(tableName, tableEl);
        var table = new TableMapping
        {
            TableKey = tableName,
            TranslationKey = translationKey,
            ClassName = className,
        };
        var classPtr = ResolveClassPtr(className);
        if (classPtr == IntPtr.Zero)
        {
            Logger.Warn($"[MasterMapping] type not resolved: {className}, table skipped");
            return table; // 空表，整表跳过
        }

        if (tableEl.ValueKind != JsonValueKind.Object)
        {
            Logger.Warn($"[MasterMapping] table rule is not an object: {tableName}");
            return table;
        }

        foreach (var fieldProp in tableEl.EnumerateObject())
        {
            if (fieldProp.Name.StartsWith("_", StringComparison.Ordinal))
                continue;

            if (!TryParseFieldConfig(fieldProp.Value, out bool seal))
                continue;

            var fieldPtr = IL2CPP.GetIl2CppField(classPtr, fieldProp.Name);
            if (fieldPtr == IntPtr.Zero)
            {
                Logger.Warn(
                    $"[MasterMapping] field not found: {className}.{fieldProp.Name}, skipped"
                );
                continue; // 字段预检失败：剔除，不进映射
            }

            table.Fields.Add(
                new FieldEntry
                {
                    Name = fieldProp.Name,
                    FieldPtr = fieldPtr,
                    Offset = (int)IL2CPP.il2cpp_field_get_offset(fieldPtr),
                    Seal = seal,
                }
            );
        }
        return table;
    }

    private static string ReadTranslationKey(string tableName, JsonElement tableEl)
    {
        if (
            tableEl.ValueKind == JsonValueKind.Object
            && tableEl.TryGetProperty("_translation_table", out var translationEl)
            && translationEl.ValueKind == JsonValueKind.String
        )
        {
            var translationKey = translationEl.GetString();
            if (!string.IsNullOrEmpty(translationKey))
                return translationKey;
        }

        return tableName;
    }

    /// <summary>字段值为 true 或对象时启用；对象可附带 seal=true。</summary>
    private static bool TryParseFieldConfig(JsonElement el, out bool seal)
    {
        seal = false;
        if (el.ValueKind == JsonValueKind.True)
            return true;
        if (el.ValueKind == JsonValueKind.False || el.ValueKind == JsonValueKind.Null)
            return false;
        if (el.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var prop in el.EnumerateObject())
        {
            if (prop.NameEquals("seal") && prop.Value.ValueKind == JsonValueKind.True)
                seal = true;
        }
        return true;
    }

    private static int CountFields(Dictionary<string, TableMapping> tables)
    {
        int n = 0;
        foreach (var t in tables.Values)
            n += t.Fields.Count;
        return n;
    }

    private static List<string> ReadFlatTypes(JsonElement root)
    {
        var result = new List<string>();
        if (!root.TryGetProperty("flat_types", out var flatTypesEl))
            return result;

        foreach (var item in flatTypesEl.EnumerateArray())
        {
            var type = item.GetString();
            if (!string.IsNullOrEmpty(type))
                result.Add(type);
        }
        return result;
    }

    private static void AddContentType(List<string> contentTypes, HashSet<string> seen, string type)
    {
        if (!string.IsNullOrEmpty(type) && seen.Add(type))
            contentTypes.Add(type);
    }

    private static string ToClassName(string tableName)
    {
        string name = tableName.StartsWith("m_", StringComparison.Ordinal)
            ? tableName.Substring(2)
            : tableName;

        var sb = new StringBuilder("M");
        bool upper = true;
        foreach (char ch in name)
        {
            if (ch == '_')
            {
                upper = true;
                continue;
            }

            sb.Append(upper ? char.ToUpperInvariant(ch) : ch);
            upper = false;
        }
        return sb.ToString();
    }

    /// <summary>按表名解析 Il2Cpp 类指针。直接用 native API，不依赖托管侧反射查找。</summary>
    private static IntPtr ResolveClassPtr(string className)
    {
        try
        {
            // 直接用 IL2CPP native API 按 (assembly, namespace, className) 查类指针，
            // 等价于编译期生成的 MNetherCodes 静态构造里的 GetIl2CppClass 调用。
            // 绕过 AppDomain.GetAssemblies —— BepInEx IL2CPP 的 interop 程序集不在那里。
            return IL2CPP.GetIl2CppClass("Project.dll", "Project.Master.NoaMessagePack", className);
        }
        catch (Exception e)
        {
            Logger.Error($"[MasterMapping] resolve {className} failed: {e.Message}");
            return IntPtr.Zero;
        }
    }

    public static int GetArrayLength(IntPtr arrayPtr) =>
        arrayPtr == IntPtr.Zero ? 0 : (int)IL2CPP.il2cpp_array_length(arrayPtr);

    public static IntPtr GetArrayStartPointer(IntPtr arrayPtr)
    {
        // IL2CPP 引用类型数组布局：[对象头(2*sizeof(IntPtr))][bounds(2*sizeof(IntPtr))][元素0]...
        // 与 Il2CppArrayBase.ArrayStartPointer 公式一致
        int headerSize = 4 * IntPtr.Size;
        return arrayPtr + headerSize;
    }

    public static unsafe IntPtr GetArrayElement(IntPtr arrayStartPtr, int index) =>
        *(IntPtr*)(arrayStartPtr + index * IntPtr.Size);

    /// <summary>
    /// 读取 row 上指定字段当前值。直接操作 native 指针，等价编译期属性 getter。
    /// </summary>
    public static unsafe string ReadField(IntPtr rowPtr, FieldEntry entry)
    {
        var ptr = *(IntPtr*)(rowPtr + entry.Offset);
        return ptr == IntPtr.Zero ? null : IL2CPP.Il2CppStringToManaged(ptr);
    }

    /// <summary>
    /// 写入 row 上指定字段。必须走 il2cpp_gc_wbarrier_set_field，否则 GC 会漏扫引用。
    /// </summary>
    public static unsafe void WriteField(IntPtr rowPtr, FieldEntry entry, string value)
    {
        IL2CPP.il2cpp_gc_wbarrier_set_field(
            rowPtr,
            rowPtr + entry.Offset,
            IL2CPP.ManagedStringToIl2Cpp(value)
        );
    }
}
