using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Absf.Master;
using Project.Master;

namespace AbyssMod.Services;

/// <summary>
/// 通过 Il2CppInterop 生成的托管类型，将静态翻译写入已加载的 MasterData 缓存。
/// </summary>
internal sealed class MasterDataTranslator
{
    private const string ModelNamespace = "Project.Master.NoaMessagePack.";

    private static readonly HashSet<(string Table, string Field)> SealFields = new()
    {
        ("m_ability_details", "description"),
        ("m_ability_details", "awake_description"),
        ("m_character_action_skills", "description"),
    };

    private static readonly MethodInfo CastMethod = typeof(IMasterLoadResult)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method =>
            method.Name == nameof(IMasterLoadResult.Cast)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 0
        );

    private readonly IReadOnlyDictionary<string, TableTranslator> _tables;

    private MasterDataTranslator(IReadOnlyDictionary<string, TableTranslator> tables)
    {
        _tables = tables;
    }

    public int TableCount => _tables.Count;

    public static MasterDataTranslator Create(
        IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, string>>> tables
    )
    {
        var translators = new Dictionary<string, TableTranslator>(StringComparer.Ordinal);

        foreach (var (tableName, fields) in tables)
        {
            try
            {
                var translator = CreateTableTranslator(tableName, fields);
                if (translator != null)
                    translators[translator.CacheTypeName] = translator;
            }
            catch (Exception e)
            {
                Logger.Warn(
                    $"MasterData translation plan failed [{tableName}]: {Unwrap(e).Message}"
                );
            }
        }

        Logger.Info($"MasterData translation plan created. Tables: {translators.Count}");
        return new MasterDataTranslator(translators);
    }

    public bool Contains(Il2CppSystem.Type cacheType) =>
        cacheType != null && _tables.ContainsKey(cacheType.Name);

    public bool TryTranslate(
        Il2CppSystem.Type cacheType,
        IMasterLoadResult result,
        out int translatedEntries
    )
    {
        translatedEntries = 0;
        if (
            cacheType == null
            || result == null
            || !_tables.TryGetValue(cacheType.Name, out var translator)
        )
            return false;

        try
        {
            translatedEntries = translator.Translate(result);
        }
        catch (Exception e)
        {
            Logger.Warn(
                $"MasterData table translation failed [{translator.TableName}]: {Unwrap(e).Message}"
            );
        }
        return true;
    }

    private static TableTranslator CreateTableTranslator(
        string tableName,
        Dictionary<string, Dictionary<string, string>> fields
    )
    {
        var rowType = typeof(MasterDataStore).Assembly.GetType(
            ModelNamespace + ToClassName(tableName),
            throwOnError: false
        );
        if (rowType == null)
        {
            Logger.Warn($"MasterData type not found for translation table: {tableName}");
            return null;
        }

        var bindings = new List<FieldBinding>();
        foreach (var (fieldName, translations) in fields)
        {
            if (translations == null || translations.Count == 0)
                continue;

            var property = rowType.GetProperty(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public
            );
            if (
                property == null
                || property.PropertyType != typeof(string)
                || property.GetMethod?.IsPublic != true
                || property.SetMethod?.IsPublic != true
            )
            {
                Logger.Warn($"MasterData string property not found: {rowType.Name}.{fieldName}");
                continue;
            }

            try
            {
                bindings.Add(
                    CreateFieldBinding(
                        rowType,
                        property,
                        translations,
                        SealFields.Contains((tableName, fieldName))
                    )
                );
            }
            catch (Exception e)
            {
                Logger.Warn(
                    $"MasterData property binding failed [{rowType.Name}.{fieldName}]: {Unwrap(e).Message}"
                );
            }
        }

        return bindings.Count == 0
            ? null
            : new TableTranslator(tableName, rowType.Name, CreateRowsGetter(rowType), bindings);
    }

    private static Func<IMasterLoadResult, IEnumerable> CreateRowsGetter(Type rowType)
    {
        var result = Expression.Parameter(typeof(IMasterLoadResult), "result");
        var loadResultType = typeof(MasterLoadResult<>).MakeGenericType(rowType);
        var cast = Expression.Call(result, CastMethod.MakeGenericMethod(loadResultType));
        var rows = Expression.Property(cast, "Rows");
        return Expression
            .Lambda<Func<IMasterLoadResult, IEnumerable>>(
                Expression.Convert(rows, typeof(IEnumerable)),
                result
            )
            .Compile();
    }

    private static FieldBinding CreateFieldBinding(
        Type rowType,
        PropertyInfo property,
        Dictionary<string, string> translations,
        bool normalizeSealNames
    )
    {
        var row = Expression.Parameter(typeof(object), "row");
        var value = Expression.Parameter(typeof(string), "value");
        var target = Expression.Convert(row, rowType);
        var member = Expression.Property(target, property);

        var get = Expression.Lambda<Func<object, string>>(member, row).Compile();
        var set = Expression
            .Lambda<Action<object, string>>(Expression.Assign(member, value), row, value)
            .Compile();
        return new FieldBinding(
            get,
            set,
            normalizeSealNames ? NormalizeTranslations(translations) : translations
        );
    }

    private static string ToClassName(string tableName)
    {
        string name = tableName.StartsWith("m_", StringComparison.Ordinal)
            ? tableName.Substring(2)
            : tableName;
        var result = new StringBuilder("M");
        bool upper = true;

        foreach (char ch in name)
        {
            if (ch == '_')
            {
                upper = true;
                continue;
            }

            result.Append(upper ? char.ToUpperInvariant(ch) : ch);
            upper = false;
        }
        return result.ToString();
    }

    private static string NormalizeTranslation(string text) =>
        text.Replace("纹章：冲击", "紋章：衝撃", StringComparison.Ordinal)
            .Replace("纹章：热情", "紋章：情熱", StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> NormalizeTranslations(
        Dictionary<string, string> translations
    )
    {
        var normalized = new Dictionary<string, string>(translations.Count, StringComparer.Ordinal);
        foreach (var (source, translated) in translations)
            normalized[source] = string.IsNullOrEmpty(translated)
                ? translated
                : NormalizeTranslation(translated);
        return normalized;
    }

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException
            : exception;

    private sealed class TableTranslator
    {
        private readonly Func<IMasterLoadResult, IEnumerable> _getRows;
        private readonly IReadOnlyList<FieldBinding> _fields;

        public TableTranslator(
            string tableName,
            string cacheTypeName,
            Func<IMasterLoadResult, IEnumerable> getRows,
            IReadOnlyList<FieldBinding> fields
        )
        {
            TableName = tableName;
            CacheTypeName = cacheTypeName;
            _getRows = getRows;
            _fields = fields;
        }

        public string TableName { get; }
        public string CacheTypeName { get; }

        public int Translate(IMasterLoadResult result)
        {
            var rows = _getRows(result);
            int count = 0;
            foreach (var row in rows)
            {
                if (row == null)
                    continue;

                foreach (var field in _fields)
                {
                    string original = field.Get(row);
                    if (
                        !string.IsNullOrEmpty(original)
                        && field.Translations.TryGetValue(original, out string translated)
                        && !string.IsNullOrEmpty(translated)
                        && !string.Equals(original, translated, StringComparison.Ordinal)
                    )
                    {
                        field.Set(row, translated);
                        count++;
                    }
                }
            }
            return count;
        }
    }

    private sealed class FieldBinding
    {
        public FieldBinding(
            Func<object, string> get,
            Action<object, string> set,
            IReadOnlyDictionary<string, string> translations
        )
        {
            Get = get;
            Set = set;
            Translations = translations;
        }

        public Func<object, string> Get { get; }
        public Action<object, string> Set { get; }
        public IReadOnlyDictionary<string, string> Translations { get; }
    }
}
