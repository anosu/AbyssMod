using System;
using System.Collections.Generic;
using Absf;
using Absf.Novel;
using AbyssMod.Services;
using HarmonyLib;
using Project;
using Project.Library;
using Project.MainStory;
using Project.Novel;
using Project.Outgame;
using Project.User;
using TMPro;
using UnityEngine;

namespace AbyssMod.Patches;

using Il2CppNovelInfo = Il2CppSystem.ValueTuple<string, string>;
using NovelLogList = Il2CppSystem.Collections.Generic.List<NovelLogData>;
using TranslationTable = Dictionary<string, string>;

/// <summary>
/// 剧情与 UI 文本翻译补丁：覆盖标题、人名、对话及所有 TMP 文本。
/// </summary>
[HarmonyPatch]
public static class TranslationPatch
{
    private const string TitlesTable = "titles";
    private const string DescriptionsTable = "descriptions";
    private const string NamesTable = "names";
    private const string UserPlaceholder = "<user>";
    private const string HiddenUserPlaceholder = "%user%";

    private static NovelController _novelController;
    private static NovelViewMessageWindow _messageWindow;
    private static string _currentOriginalMessage;
    private static string _currentTranslatedMessage;
    private static bool _refreshingCurrentMessage;
    private static bool _uiTextLoadRequested;
    private static bool _uiTextErrorLogged;
    private static TranslationTable _cachedUiTextTable;
    private static int _cachedUiTextCount;
    private static HashSet<string> _cachedTranslatedUiTexts;

    private static string NovelId => _novelController?._common?.ScriptId ?? string.Empty;

    private static bool CanTranslate() => Config.Translation.Value && Plugin.Trans != null;

    // UI 文本翻译

    private static string TranslateTmpText(TMP_Text textComponent, string sourceText)
    {
        if (!CanTranslate() || string.IsNullOrEmpty(sourceText))
            return sourceText;

        var uiTextTable = GetUiTextTable();
        if (uiTextTable == null || uiTextTable.Count == 0)
            return sourceText;

        if (IsKnownUiTranslation(uiTextTable, sourceText))
            return sourceText;

        if (
            uiTextTable.TryGetValue(sourceText, out string translatedText)
            && !string.IsNullOrEmpty(translatedText)
        )
            return translatedText;

        string transformPath = GetTransformPath(textComponent?.transform);
        if (
            !string.IsNullOrEmpty(transformPath)
            && uiTextTable.TryGetValue(transformPath, out translatedText)
            && !string.IsNullOrEmpty(translatedText)
        )
            return translatedText;

        return sourceText;
    }

    private static bool IsKnownUiTranslation(TranslationTable uiTextTable, string sourceText)
    {
        if (
            _cachedTranslatedUiTexts == null
            || !ReferenceEquals(_cachedUiTextTable, uiTextTable)
            || _cachedUiTextCount != uiTextTable.Count
        )
        {
            _cachedUiTextTable = uiTextTable;
            _cachedUiTextCount = uiTextTable.Count;
            _cachedTranslatedUiTexts = new HashSet<string>(
                uiTextTable.Values,
                StringComparer.Ordinal
            );
        }

        return _cachedTranslatedUiTexts.Contains(sourceText);
    }

    private static TranslationTable GetUiTextTable()
    {
        if (!_uiTextLoadRequested)
        {
            _uiTextLoadRequested = true;
            try
            {
                _ = Plugin.Trans.EnsureStaticTranslationsLoadedAsync();
            }
            catch (Exception e)
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

        var pathParts = new Stack<string>();
        for (var current = transform; current != null; current = current.parent)
            pathParts.Push(current.name);

        return string.Join("/", pathParts);
    }

    // TMP 文本注入

    private static string TranslateTmpTextSafely(TMP_Text textComponent, string sourceText)
    {
        try
        {
            return TranslateTmpText(textComponent, sourceText);
        }
        catch (Exception e)
        {
            if (!_uiTextErrorLogged)
            {
                _uiTextErrorLogged = true;
                Logger.Warn($"UI text translation failed; further errors suppressed: {e.Message}");
            }
        }
        return sourceText;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TMP_Text), "set_text")]
    public static void TranslateTextSetter(TMP_Text __instance, ref string value) =>
        value = TranslateTmpTextSafely(__instance, value);

    [HarmonyPrefix, HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string))]
    public static void TranslateSetText(TMP_Text __instance, ref string sourceText) =>
        sourceText = TranslateTmpTextSafely(__instance, sourceText);

    [
        HarmonyPrefix,
        HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(bool))
    ]
    public static bool TranslateSetTextAndSyncInputBox(TMP_Text __instance, ref string sourceText)
    {
        __instance.text = TranslateTmpTextSafely(__instance, sourceText);
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
        string translatedText = TranslateTmpTextSafely(text, text.text);
        if (!string.Equals(translatedText, text.text, StringComparison.Ordinal))
            text.text = translatedText;
    }

    // 通用翻译查询辅助

    private static string TranslateFrom(TranslationTable table, string sourceText)
    {
        if (string.IsNullOrEmpty(sourceText) || table == null)
            return sourceText;
        return
            table.TryGetValue(sourceText, out var translated) && !string.IsNullOrEmpty(translated)
            ? translated
            : sourceText;
    }

    private static string TranslateStatic(string tableName, string sourceText) =>
        TranslateFrom(Plugin.Trans.GetTable(tableName), sourceText);

    private static string TranslateStaticForCurrentNovel(string tableName, string sourceText) =>
        CanTranslate() && TryGetNovel(NovelId, out _)
            ? TranslateStatic(tableName, sourceText)
            : sourceText;

    private static string TranslateCurrentNovelText(string sourceText) =>
        CanTranslate() && TryGetNovel(NovelId, out var translation)
            ? TranslateFrom(translation, sourceText)
            : sourceText;

    private static bool TryGetNovel(string novelId, out TranslationTable translation)
    {
        translation = null;
        return Plugin.Trans != null
            && !string.IsNullOrEmpty(novelId)
            && Plugin.Trans.Novels.TryGetValue(novelId, out translation);
    }

    private static string SelectNovelMessageVariant(
        TranslationTable translation,
        string sourceText,
        bool targetTranslated,
        string displayName
    )
    {
        if (string.IsNullOrEmpty(sourceText) || translation == null)
            return sourceText;

        string lookupValue = string.IsNullOrEmpty(displayName)
            ? sourceText
            : sourceText.Replace(displayName, UserPlaceholder, StringComparison.Ordinal);

        if (targetTranslated)
        {
            if (
                translation.TryGetValue(lookupValue, out string translatedText)
                && !string.IsNullOrEmpty(translatedText)
            )
                return ExpandUserPlaceholder(translatedText, displayName);

            return sourceText;
        }

        foreach (var entry in translation)
        {
            if (string.Equals(entry.Value, lookupValue, StringComparison.Ordinal))
                return ExpandUserPlaceholder(entry.Key, displayName);
        }

        return sourceText;
    }

    private static void CaptureCurrentNovelMessages(TranslationTable translation, string message)
    {
        string displayName = GetDisplayUserName();
        _currentOriginalMessage = SelectNovelMessageVariant(
            translation,
            message,
            targetTranslated: false,
            displayName: displayName
        );
        _currentTranslatedMessage = SelectNovelMessageVariant(
            translation,
            message,
            targetTranslated: true,
            displayName: displayName
        );
    }

    private static string GetConfiguredCurrentMessage() =>
        Config.Translation.Value ? _currentTranslatedMessage : _currentOriginalMessage;

    private static string GetDisplayUserName()
    {
        try
        {
            string userName = Engine.Get<UserData>().UserStatus.Name.Value;
            return StringUtility.ToDisplayUserName(userName);
        }
        catch
        {
            return null;
        }
    }

    private static string ExpandUserPlaceholder(string value, string displayName)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(displayName))
            return value;
        return value.Replace(UserPlaceholder, displayName, StringComparison.Ordinal);
    }

    private static bool ContainsUserPlaceholder(string value) =>
        !string.IsNullOrEmpty(value) && value.Contains(UserPlaceholder, StringComparison.Ordinal);

    private static string HideUserPlaceholder(string value) =>
        value?.Replace(UserPlaceholder, HiddenUserPlaceholder, StringComparison.Ordinal);

    private static string RestoreUserPlaceholder(string value) =>
        value?.Replace(HiddenUserPlaceholder, UserPlaceholder, StringComparison.Ordinal);

    private static void EnsureCurrentNovelTranslationLoaded()
    {
        if (
            CanTranslate()
            && !string.IsNullOrEmpty(NovelId)
            && !Plugin.Trans.Novels.ContainsKey(NovelId)
        )
            Plugin.Trans.GetNovelTranslationAsync(NovelId).Wait();
    }

    private static void ResetCurrentMessageState()
    {
        _messageWindow = null;
        _currentOriginalMessage = null;
        _currentTranslatedMessage = null;
        _refreshingCurrentMessage = false;
    }

    private static NovelViewMessageWindow GetMessageWindow()
    {
        if (_messageWindow == null || !_messageWindow.gameObject.activeInHierarchy)
            _messageWindow = UnityEngine.Object.FindObjectOfType<NovelViewMessageWindow>();

        return _messageWindow;
    }

    public static void RefreshCurrentMessage()
    {
        try
        {
            EnsureCurrentNovelTranslationLoaded();

            if (!TryGetNovel(NovelId, out var translation))
                return;

            var messageWindow = GetMessageWindow();
            if (messageWindow == null)
            {
                Logger.Info("Current novel message refresh skipped: no message window");
                return;
            }

            string current = messageWindow._messageData?.Message;
            if (string.IsNullOrEmpty(current))
                current = GetConfiguredCurrentMessage();

            if (!string.IsNullOrEmpty(current))
                CaptureCurrentNovelMessages(translation, current);

            string selected = GetConfiguredCurrentMessage();

            if (string.IsNullOrEmpty(selected) && messageWindow._messageData != null)
                selected = SelectNovelMessageVariant(
                    translation,
                    messageWindow._messageData.Message,
                    Config.Translation.Value,
                    GetDisplayUserName()
                );

            if (string.IsNullOrEmpty(selected))
            {
                Logger.Info("Current novel message refresh skipped: no captured message");
                return;
            }

            _refreshingCurrentMessage = true;
            try
            {
                if (messageWindow._messageData != null)
                    messageWindow._messageData.Message = selected;
                messageWindow.SetText(selected);
            }
            finally
            {
                _refreshingCurrentMessage = false;
            }

            Logger.Info("Current novel message refreshed");
        }
        catch (Exception e)
        {
            Logger.Warn($"Current novel message refresh failed: {e.Message}");
        }
    }

    // 剧情翻译 Harmony 补丁

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelController), nameof(NovelController.InitNovel))]
    public static void InitNovelController(NovelController __instance)
    {
        _novelController = __instance;
        ResetCurrentMessageState();
    }

    /// <summary>
    /// 剧情目录解析后触发翻译加载。.Wait() 阻塞主线程是刻意为之，
    /// 必须确保翻译数据在后续文本渲染前就绪。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelPathUtility), nameof(NovelPathUtility.GetNovelScenarioDirectory))]
    public static void SetupTranslation(string novelId)
    {
        if (!CanTranslate())
            return;

        Logger.Info($"NovelId: {novelId}");
        Plugin.Trans.GetNovelTranslationAsync(novelId).Wait();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelScriptInfoUtility), nameof(NovelScriptInfoUtility.GetScriptInfo))]
    public static void TranslateTitleAndDescription(Il2CppNovelInfo __result)
    {
        __result.Item1 = TranslateStaticForCurrentNovel(TitlesTable, __result.Item1);
        __result.Item2 = TranslateStaticForCurrentNovel(DescriptionsTable, __result.Item2);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelTitle), nameof(NovelTitle.SetTitle))]
    public static void TranslateTitle(ref string title)
    {
        title = TranslateStaticForCurrentNovel(TitlesTable, title);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelViewMessageWindow), nameof(NovelViewMessageWindow.SetName))]
    public static void TranslateSpeakerName(ref string name)
    {
        name = TranslateStaticForCurrentNovel(NamesTable, name);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelViewMessageWindow), nameof(NovelViewMessageWindow.SetText))]
    public static void TrackMessageWindow(NovelViewMessageWindow __instance)
    {
        _messageWindow = __instance;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelText), nameof(NovelText.Parse))]
    public static void TranslateNovelText(ref string message)
    {
        if (TryGetNovel(NovelId, out var translation))
        {
            CaptureCurrentNovelMessages(translation, message);
            message = GetConfiguredCurrentMessage();
        }
        else
        {
            _currentOriginalMessage = message;
            _currentTranslatedMessage = message;
        }
    }

    /// <summary>
    /// 日志记录时将 &lt;user&gt; 替换为占位符以避免翻译系统误处理用户名。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelModelMessageLog), nameof(NovelModelMessageLog.Add))]
    public static bool NormalizeLogPlaceholders(ref string charaName, ref string message)
    {
        if (_refreshingCurrentMessage)
            return false;

        charaName = HideUserPlaceholder(charaName);
        message = HideUserPlaceholder(message);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelLogPopup), nameof(NovelLogPopup.SetData))]
    public static void TranslateLogEntries(ref NovelLogList dataList)
    {
        var list = new NovelLogList();

        foreach (var data in dataList)
        {
            string name = RestoreUserPlaceholder(data.Name);
            string message = RestoreUserPlaceholder(data.Message);

            if (CanTranslate() && TryGetNovel(data.ScriptId, out var translation))
            {
                name = TranslateStatic(NamesTable, name);
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
    public static void TranslateBalloonMessage(CommandDotMessageData messageData)
    {
        messageData.Message = TranslateCurrentNovelText(messageData.Message);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(NovelCmdMessageTextCenter),
        nameof(NovelCmdMessageTextCenter.OnCommandStartASync)
    )]
    public static void HideCenterTextUserPlaceholder(NovelArguments args)
    {
        string message = args.GetString(2);
        if (ContainsUserPlaceholder(message))
            args._list[2] = NovelArgument.SetString(HideUserPlaceholder(message));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelModelMessageText), nameof(NovelModelMessageText.SetMessage))]
    public static void TranslateMessageText(CommandMessageTextData data)
    {
        data.Message = RestoreUserPlaceholder(data.Message);

        data.Message = TranslateCurrentNovelText(data.Message);

        if (ContainsUserPlaceholder(data.Message))
            data.Message = ExpandUserPlaceholder(data.Message, GetDisplayUserName());
    }
}
