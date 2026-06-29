using System.Collections.Generic;
using AbyssMod.Services;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Threading;
using Project.Library;
using Project.MainStory;
using Project.Novel;
using Project.Outgame;
using TMPro;
using UnityEngine;

namespace AbyssMod.Patches;

using StringDictionary = System.Collections.Generic.Dictionary<string, string>;
using StringStack = System.Collections.Generic.Stack<string>;

/// <summary>
/// 剧情与 UI 文本翻译补丁：覆盖标题、人名、对话及所有 TMP 文本。
/// </summary>
[HarmonyPatch]
public static class TranslationPatch
{
    private static NovelController _novelController;
    private static bool _uiTextsLoadRequested;
    private static bool _uiTextErrorLogged;
    private static HashSet<string> _uiTextValueSet;

    private static string NovelId => _novelController?._common?.ScriptId ?? string.Empty;

    // ═══════════════════════════════════════
    //  UI 文本翻译
    // ═══════════════════════════════════════

    private static string TranslateUiText(TMP_Text text, string value)
    {
        if (!Config.Translation.Value || Plugin.Trans == null || string.IsNullOrEmpty(value))
            return value;

        var uiTexts = GetUiTextTable();
        if (uiTexts == null || uiTexts.Count == 0)
            return value;

        // 已是译文则跳过（O(1) 集合查找替代原有的 O(n) 字典值扫描）
        if (_uiTextValueSet == null)
            _uiTextValueSet = new HashSet<string>(uiTexts.Values, System.StringComparer.Ordinal);
        if (_uiTextValueSet.Contains(value))
            return value;

        // 直接文本匹配
        if (uiTexts.TryGetValue(value, out string translated) && !string.IsNullOrEmpty(translated))
            return translated;

        // Transform 层级路径匹配
        string path = GetTransformPath(text?.transform);
        if (
            !string.IsNullOrEmpty(path)
            && uiTexts.TryGetValue(path, out translated)
            && !string.IsNullOrEmpty(translated)
        )
            return translated;

        return value;
    }

    private static StringDictionary GetUiTextTable()
    {
        if (!_uiTextsLoadRequested)
        {
            _uiTextsLoadRequested = true;
            try
            {
                _ = Plugin.Trans.EnsureStaticTranslationsLoadedAsync();
            }
            catch (System.Exception e)
            {
                Logger.Warn($"UI text translation load request skipped: {e.Message}");
            }
        }
        return Plugin.Trans.GetTable(TranslationPaths.UiTexts);
    }

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
            return null;

        var names = new StringStack();
        for (var current = transform; current != null; current = current.parent)
            names.Push(current.name);

        return string.Join("/", names);
    }

    // ═══════════════════════════════════════
    //  TMP 文本注入 — 4 入口复用同一核心
    // ═══════════════════════════════════════

    private static string TranslateOrKeep(TMP_Text instance, string value)
    {
        try
        {
            return TranslateUiText(instance, value);
        }
        catch (System.Exception e)
        {
            LogUiTextErrorOnce(e);
        }
        return value;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TMP_Text), "set_text")]
    public static void SetUiText(TMP_Text __instance, ref string value) =>
        value = TranslateOrKeep(__instance, value);

    [HarmonyPrefix, HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string))]
    public static void SetUiTextBySetText(TMP_Text __instance, ref string sourceText) =>
        sourceText = TranslateOrKeep(__instance, sourceText);

    [
        HarmonyPrefix,
        HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(bool))
    ]
    public static bool SetUiTextBySetTextSync(
        TMP_Text __instance,
        ref string sourceText,
        bool syncTextInputBox
    )
    {
        __instance.text = TranslateOrKeep(__instance, sourceText);
        return false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TextMeshProUGUI), nameof(TextMeshProUGUI.OnEnable))]
    public static void TranslateStaticUiText(TextMeshProUGUI __instance) =>
        TranslateOnEnable(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(TextMeshPro), nameof(TextMeshPro.OnEnable))]
    public static void TranslateStaticUiText(TextMeshPro __instance) =>
        TranslateOnEnable(__instance);

    private static void TranslateOnEnable(TMP_Text text)
    {
        if (text == null)
            return;
        string translated = TranslateOrKeep(text, text.text);
        if (!string.Equals(translated, text.text, System.StringComparison.Ordinal))
            text.text = translated;
    }

    private static void LogUiTextErrorOnce(System.Exception e)
    {
        if (_uiTextErrorLogged)
            return;
        _uiTextErrorLogged = true;
        Logger.Warn($"UI text translation failed; further errors suppressed: {e.Message}");
    }

    // ═══════════════════════════════════════
    //  通用翻译查询辅助
    // ═══════════════════════════════════════

    private static string TranslateFrom(StringDictionary table, string value)
    {
        if (string.IsNullOrEmpty(value) || table == null)
            return value;
        return table.TryGetValue(value, out var t) && !string.IsNullOrEmpty(t) ? t : value;
    }

    private static string TranslateStatic(string tableName, string value) =>
        TranslateFrom(Plugin.Trans.GetTable(tableName), value);

    private static bool HasCurrentNovel() =>
        Config.Translation.Value && Plugin.Trans.Novels.ContainsKey(NovelId);

    public static bool TryGetCurrentNovel(out StringDictionary translation)
    {
        translation = null;
        return HasCurrentNovel() && Plugin.Trans.Novels.TryGetValue(NovelId, out translation);
    }

    // ═══════════════════════════════════════
    //  剧情翻译 Harmony 补丁
    // ═══════════════════════════════════════

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelController), nameof(NovelController.InitNovel))]
    public static void InitNovelController(NovelController __instance)
    {
        _novelController = __instance;
    }

    /// <summary>
    /// 剧情目录解析后触发翻译加载。.Wait() 阻塞主线程是刻意为之 —
    /// 必须确保翻译数据在后续文本渲染前就绪。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelPathUtility), nameof(NovelPathUtility.GetNovelScenarioDirectory))]
    public static void SetupTranslation(string novelId)
    {
        if (!Config.Translation.Value)
            return;

        Logger.Info($"NovelId: {novelId}");
        Plugin.Trans.GetNovelTranslationAsync(novelId).Wait();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelScriptInfoUtility), nameof(NovelScriptInfoUtility.GetScriptInfo))]
    public static void SetTitleAndDescription(ValueTuple<string, string> __result)
    {
        if (HasCurrentNovel())
        {
            __result.Item1 = TranslateStatic("titles", __result.Item1);
            __result.Item2 = TranslateStatic("descriptions", __result.Item2);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelTitle), nameof(NovelTitle.SetTitle))]
    public static void SetTitle(ref string title)
    {
        if (HasCurrentNovel())
            title = TranslateStatic("titles", title);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelViewMessageWindow), nameof(NovelViewMessageWindow.SetName))]
    public static void SetName(ref string name)
    {
        if (HasCurrentNovel())
            name = TranslateStatic("names", name);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelText), nameof(NovelText.Parse))]
    public static void SetText(List<Letter> letters, ref string message)
    {
        if (TryGetCurrentNovel(out var translation))
            message = TranslateFrom(translation, message);
    }

    /// <summary>
    /// 日志记录时将 &lt;user&gt; 替换为占位符以避免翻译系统误处理用户名。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelModelMessageLog), nameof(NovelModelMessageLog.Add))]
    public static void SetLogAdd(
        string scriptId,
        string assetId,
        ref string charaName,
        ref string message,
        string logId,
        NovelSound voice,
        CancellationToken ct
    )
    {
        charaName = charaName?.Replace("<user>", "%user%");
        message = message?.Replace("<user>", "%user%");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelLogPopup), nameof(NovelLogPopup.SetData))]
    public static void SetLog(ref List<NovelLogData> dataList)
    {
        var list = new List<NovelLogData>();
        bool hasNovel = TryGetCurrentNovel(out var translation);

        foreach (var data in dataList)
        {
            string name = data.Name?.Replace("%user%", "<user>");
            string message = data.Message?.Replace("%user%", "<user>");

            if (hasNovel)
            {
                name = TranslateStatic("names", name);
                message = TranslateFrom(translation, message);
            }

            list.Add(
                new NovelLogData(
                    data.ScriptId,
                    data.AssetId,
                    name,
                    message,
                    data.LogId,
                    data.Voice,
                    data.Ct
                )
            );
        }
        dataList = list;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelModelDotBalloon), nameof(NovelModelDotBalloon.StartBalloonMessage))]
    public static void SetBalloon(CommandDotMessageData messageData)
    {
        if (TryGetCurrentNovel(out var translation))
            messageData.Message = TranslateFrom(translation, messageData.Message);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(NovelMessageTextComponent),
        nameof(NovelMessageTextComponent.SetMessageText)
    )]
    public static void SetTextCenter(NovelModelCommon common, CommandMessageTextData data)
    {
        if (TryGetCurrentNovel(out var translation))
            data.Message = TranslateFrom(translation, data.Message);
    }
}
