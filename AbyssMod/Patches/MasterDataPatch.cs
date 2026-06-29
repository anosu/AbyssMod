using System;
using System.Collections.Generic;
using AbyssMod.Services;
using HarmonyLib;
using Project.Master;

namespace AbyssMod.Patches;

/// <summary>
/// 在 masterdata 反序列化后、写入 MasterDataStore 缓存前替换静态文本。
/// 翻译规则由 master_mapping.json 驱动，新增表无需改本文件。
/// 剧情正文脚本不在 masterdata 内，仍由 TranslationPatch 处理。
/// </summary>
[HarmonyPatch]
public static class MasterDataPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MasterDataStore), nameof(MasterDataStore._DownloadFirstAsync_b__8_0))]
    public static void TranslateBeforeCache(
        Il2CppSystem.Type elementType,
        Il2CppSystem.Object rowsArray
    )
    {
        if (!Config.Translation.Value || Plugin.Trans == null || rowsArray == null)
            return;

        try
        {
            string typeName = elementType?.Name;
            if (typeName == null)
                return;

            if (!MasterMapping.Tables.TryGetValue(typeName, out var table))
                return; // 该表无翻译规则，等价旧代码的 _ => 0

            Plugin.Trans.EnsureStaticTranslationsLoaded();

            var arrayPtr = rowsArray.Pointer;
            if (arrayPtr == IntPtr.Zero)
                return;

            int rowCount = MasterMapping.GetArrayLength(arrayPtr);
            if (rowCount <= 0)
                return;

            var arrayStart = MasterMapping.GetArrayStartPointer(arrayPtr);
            var dictCache = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal
            );

            int count = 0;
            for (int i = 0; i < rowCount; i++)
            {
                var rowPtr = MasterMapping.GetArrayElement(arrayStart, i);
                if (rowPtr == IntPtr.Zero)
                    continue;
                count += TranslateRow(rowPtr, table, dictCache);
            }
        }
        catch (Exception e)
        {
            Logger.Error($"[MasterDataTranslation] threw: {e}");
        }
    }

    private static int TranslateRow(
        IntPtr rowPtr,
        TableMapping table,
        Dictionary<string, Dictionary<string, string>> dictCache
    )
    {
        int count = 0;
        foreach (var entry in table.Fields)
        {
            string original = MasterMapping.ReadField(rowPtr, entry);
            if (string.IsNullOrEmpty(original))
                continue;

            var dict = GetCachedTable(table.TranslationKey, entry.Name, dictCache);
            if (
                dict == null
                || !dict.TryGetValue(original, out string translated)
                || string.IsNullOrEmpty(translated)
            )
                continue;

            MasterMapping.WriteField(
                rowPtr,
                entry,
                entry.Seal ? RestoreSealNames(translated) : translated
            );
            count++;
        }
        return count;
    }

    private static Dictionary<string, string> GetCachedTable(
        string dictName,
        string fieldName,
        Dictionary<string, Dictionary<string, string>> dictCache
    )
    {
        string cacheKey = $"{dictName}\0{fieldName}";
        if (dictCache.TryGetValue(cacheKey, out var dict))
            return dict;

        dict = Plugin.Trans.GetFieldTable(dictName, fieldName);
        dictCache[cacheKey] = dict;
        return dict;
    }

    /// <summary>纹章名繁简修正：译文中混入的简体「纹章：冲击/热情」还原为游戏内的繁体写法。</summary>
    private static string RestoreSealNames(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (!text.Contains("纹章：", StringComparison.Ordinal))
            return text;

        return text.Replace("纹章：冲击", "紋章：衝撃", StringComparison.Ordinal)
            .Replace("纹章：热情", "紋章：情熱", StringComparison.Ordinal);
    }
}
