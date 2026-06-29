using AbyssMod.Services;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
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
/// 剧情翻译补丁：标题、人名、对话文本的翻译注入。
/// </summary>
[HarmonyPatch]
public static class TranslationPatch
{
    private static NovelController _novelController;
    private static bool _uiTextsLoadRequested;
    private static bool _uiTextErrorLogged;

    private static string NovelId
    {
        get => _novelController?._common?.ScriptId ?? string.Empty;
    }

    private static string TranslateUiText(TMP_Text text, string value)
    {
        if (!Config.Translation.Value || Plugin.Trans == null || value == null)
            return value;

        if (value.Length == 0)
            return value;

        var uiTexts = GetUiTextTable();
        if (uiTexts == null || uiTexts.Count == 0)
            return value;

        if (IsTranslatedUiText(uiTexts, value))
            return value;

        if (uiTexts.TryGetValue(value, out string translated) && !string.IsNullOrEmpty(translated))
            return translated;

        string path = GetTransformPath(text?.transform);
        if (
            !string.IsNullOrEmpty(path)
            && uiTexts.TryGetValue(path, out translated)
            && !string.IsNullOrEmpty(translated)
        )
            return translated;

        return value;
    }

    private static bool IsTranslatedUiText(StringDictionary uiTexts, string value)
    {
        foreach (var translation in uiTexts.Values)
        {
            if (string.Equals(translation, value, System.StringComparison.Ordinal))
                return true;
        }

        return false;
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

    private static string TranslateStaticText(string tableName, string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var table = Plugin.Trans.GetTable(tableName);
        return TryTranslate(table, value, out string translated) ? translated : value;
    }

    private static string TranslateNovelText(StringDictionary translation, string value)
    {
        return TryTranslate(translation, value, out string translated) ? translated : value;
    }

    private static bool TryTranslate(StringDictionary table, string value, out string translated)
    {
        translated = null;
        return !string.IsNullOrEmpty(value)
            && table != null
            && table.TryGetValue(value, out translated)
            && !string.IsNullOrEmpty(translated);
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelController), nameof(NovelController.InitNovel))]
    public static void InitNovelController(NovelController __instance)
    {
        _novelController = __instance;
    }

    public static bool TryGetCurrentNovel(out StringDictionary translation)
    {
        translation = null;
        return Config.Translation.Value
            && Plugin.Trans.Novels.TryGetValue(NovelId, out translation);
    }

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
        if (TryGetCurrentNovel(out var _))
        {
            __result.Item1 = TranslateStaticText("titles", __result.Item1);
            __result.Item2 = TranslateStaticText("descriptions", __result.Item2);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelTitle), nameof(NovelTitle.SetTitle))]
    public static void SetTitle(ref string title)
    {
        if (TryGetCurrentNovel(out var _))
            title = TranslateStaticText("titles", title);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelViewMessageWindow), nameof(NovelViewMessageWindow.SetName))]
    public static void SetName(ref string name)
    {
        if (TryGetCurrentNovel(out var _))
            name = TranslateStaticText("names", name);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelText), nameof(NovelText.Parse))]
    public static void SetText(List<Letter> letters, ref string message)
    {
        if (TryGetCurrentNovel(out var translation))
            message = TranslateNovelText(translation, message);
    }

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
        List<NovelLogData> list = new();
        foreach (var data in dataList)
        {
            string name = data.Name?.Replace("%user%", "<user>");
            string message = data.Message?.Replace("%user%", "<user>");

            if (TryGetCurrentNovel(out var translation))
            {
                name = TranslateStaticText("names", name);
                message = TranslateNovelText(translation, message);
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
            messageData.Message = TranslateNovelText(translation, messageData.Message);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(NovelMessageTextComponent),
        nameof(NovelMessageTextComponent.SetMessageText)
    )]
    public static void SetTextCenter(NovelModelCommon common, CommandMessageTextData data)
    {
        if (TryGetCurrentNovel(out var translation))
            data.Message = TranslateNovelText(translation, data.Message);
    }

    // Codex-added TMP UI translation: dynamic assignments pass through set_text.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TMP_Text), "set_text")]
    public static void SetUiText(TMP_Text __instance, ref string value)
    {
        try
        {
            value = TranslateUiText(__instance, value);
        }
        catch (System.Exception e)
        {
            LogUiTextTranslationErrorOnce(e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string))]
    public static void SetUiTextBySetText(TMP_Text __instance, ref string sourceText)
    {
        try
        {
            sourceText = TranslateUiText(__instance, sourceText);
        }
        catch (System.Exception e)
        {
            LogUiTextTranslationErrorOnce(e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(bool))]
    public static bool SetUiTextBySetTextSync(
        TMP_Text __instance,
        ref string sourceText,
        bool syncTextInputBox
    )
    {
        try
        {
            __instance.text = TranslateUiText(__instance, sourceText);
        }
        catch (System.Exception e)
        {
            LogUiTextTranslationErrorOnce(e);
        }

        return false;
    }

    // Codex-added TMP UI translation: prefab/static texts often only exist after OnEnable.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
    public static void TranslateStaticUiText(TextMeshProUGUI __instance)
    {
        TranslateStaticUiText((TMP_Text)__instance);
    }

    // Codex-added TMP UI translation: covers 3D/world TextMeshPro as well as UGUI.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
    public static void TranslateStaticUiText(TextMeshPro __instance)
    {
        TranslateStaticUiText((TMP_Text)__instance);
    }

    private static void TranslateStaticUiText(TMP_Text text)
    {
        if (text == null)
            return;

        try
        {
            string translated = TranslateUiText(text, text.text);
            if (!string.Equals(translated, text.text, System.StringComparison.Ordinal))
                text.text = translated;
        }
        catch (System.Exception e)
        {
            LogUiTextTranslationErrorOnce(e);
        }
    }

    private static void LogUiTextTranslationErrorOnce(System.Exception e)
    {
        if (_uiTextErrorLogged)
            return;

        _uiTextErrorLogged = true;
        Logger.Warn($"UI text translation failed; future errors will be suppressed: {e.Message}");
    }
}
