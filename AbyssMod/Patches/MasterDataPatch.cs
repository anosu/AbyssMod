using System;
using System.Collections.Generic;
using AbyssMod.Services;
using HarmonyLib;
using Project.Master;

namespace AbyssMod.Patches;

/// <summary>
/// 在 masterdata 反序列化后、写入 MasterDataStore 缓存前替换静态文本。
/// 翻译规则由 Config/master.json 驱动，新增表无需改本文件。
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
            var dictCache = new Dictionary<(string, string), Dictionary<string, string>>();

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
        Dictionary<(string, string), Dictionary<string, string>> dictCache
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

            MasterMapping.WriteField(rowPtr, entry, translated);
            count++;
        }
        return count;
    }

    private static Dictionary<string, string> GetCachedTable(
        string dictName,
        string fieldName,
        Dictionary<(string, string), Dictionary<string, string>> dictCache
    )
    {
        var key = (dictName, fieldName);
        if (dictCache.TryGetValue(key, out var dict))
            return dict;

        dict = Plugin.Trans.GetFieldTable(dictName, fieldName);
        dictCache[key] = dict;
        return dict;
    }
}
