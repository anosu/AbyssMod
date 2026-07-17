using System.Collections.Generic;
using Absf;
using Absf.Novel;
using AbyssMod.Services;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Threading;
using Project;
using Project.Library;
using Project.MainStory;
using Project.Novel;
using Project.Outgame;
using Project.User;
using TMPro;
using UnityEngine;

namespace AbyssMod.Patches;

using StringDictionary = Dictionary<string, string>;
using StringStack = Stack<string>;

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
    private const string LogUserPlaceholder = "%user%";

    private static NovelController _novelController;
    private static NovelViewMessageWindow _messageWindow;
    private static string _currentOriginalMessage;
    private static string _currentTranslatedMessage;
    private static bool _refreshingCurrentMessage;
    private static bool _uiTextsLoadRequested;
    private static bool _uiTextErrorLogged;
    private static StringDictionary _uiTextValueSetSource;
    private static int _uiTextValueSetCount;
    private static HashSet<string> _uiTextValueSet;

    private static string NovelId => _novelController?._common?.ScriptId ?? string.Empty;

    private static bool IsTranslationEnabled() => Config.Translation.Value && Plugin.Trans != null;

    // ═══════════════════════════════════════
    //  UI 文本翻译
    // ═══════════════════════════════════════

    private static string TranslateUiText(TMP_Text text, string value)
    {
        if (!IsTranslationEnabled() || string.IsNullOrEmpty(value))
            return value;

        var uiTexts = GetUiTextTable();
        if (uiTexts == null || uiTexts.Count == 0)
            return value;

        // 已是译文则跳过（O(1) 集合查找替代原有的 O(n) 字典值扫描）
        if (IsTranslatedUiTextValue(uiTexts, value))
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

    private static bool IsTranslatedUiTextValue(StringDictionary uiTexts, string value)
    {
        if (
            _uiTextValueSet == null
            || !ReferenceEquals(_uiTextValueSetSource, uiTexts)
            || _uiTextValueSetCount != uiTexts.Count
        )
        {
            _uiTextValueSetSource = uiTexts;
            _uiTextValueSetCount = uiTexts.Count;
            _uiTextValueSet = new HashSet<string>(uiTexts.Values, System.StringComparer.Ordinal);
        }

        return _uiTextValueSet.Contains(value);
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
        return table.TryGetValue(value, out var translated) && !string.IsNullOrEmpty(translated)
            ? translated
            : value;
    }

    private static string TranslateStatic(string tableName, string value) =>
        TranslateFrom(Plugin.Trans.GetTable(tableName), value);

    private static string TranslateStaticForCurrentNovel(string tableName, string value) =>
        TryGetCurrentNovel(out _) ? TranslateStatic(tableName, value) : value;

    private static string TranslateCurrentNovelText(string value) =>
        TryGetCurrentNovel(out var translation) ? TranslateFrom(translation, value) : value;

    private static bool TryGetLoadedNovel(out StringDictionary translation)
    {
        translation = null;
        return Plugin.Trans != null
            && !string.IsNullOrEmpty(NovelId)
            && Plugin.Trans.Novels.TryGetValue(NovelId, out translation);
    }

    private static string SelectNovelMessage(
        StringDictionary translation,
        string value,
        bool translated,
        string displayName
    )
    {
        if (string.IsNullOrEmpty(value) || translation == null)
            return value;

        string lookupValue = string.IsNullOrEmpty(displayName)
            ? value
            : value.Replace(displayName, UserPlaceholder, System.StringComparison.Ordinal);

        if (translated)
        {
            if (
                translation.TryGetValue(lookupValue, out string selected)
                && !string.IsNullOrEmpty(selected)
            )
                return ExpandUserPlaceholder(selected, displayName);

            return value;
        }

        foreach (var entry in translation)
        {
            if (string.Equals(entry.Value, lookupValue, System.StringComparison.Ordinal))
                return ExpandUserPlaceholder(entry.Key, displayName);
        }

        return value;
    }

    private static void CaptureCurrentNovelMessages(StringDictionary translation, string message)
    {
        string displayName = GetDisplayUserName();
        _currentOriginalMessage = SelectNovelMessage(
            translation,
            message,
            translated: false,
            displayName: displayName
        );
        _currentTranslatedMessage = SelectNovelMessage(
            translation,
            message,
            translated: true,
            displayName: displayName
        );
    }

    private static string CurrentMessageForTranslationSetting() =>
        Config.Translation.Value ? _currentTranslatedMessage : _currentOriginalMessage;

    private static string GetDisplayUserName()
    {
        try
        {
            return ReadDisplayUserName();
        }
        catch
        {
            return null;
        }
    }

    private static string ReadDisplayUserName()
    {
        string userName = Engine.Get<UserData>().UserStatus.Name.Value;
        return StringUtility.ToDisplayUserName(userName);
    }

    private static string ExpandUserPlaceholder(string value, string displayName)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(displayName))
            return value;
        return value.Replace(UserPlaceholder, displayName, System.StringComparison.Ordinal);
    }

    private static bool ContainsUserPlaceholder(string value) =>
        !string.IsNullOrEmpty(value)
        && value.Contains(UserPlaceholder, System.StringComparison.Ordinal);

    private static string HideUserPlaceholder(string value) =>
        value?.Replace(UserPlaceholder, LogUserPlaceholder, System.StringComparison.Ordinal);

    private static string RestoreUserPlaceholder(string value) =>
        value?.Replace(LogUserPlaceholder, UserPlaceholder, System.StringComparison.Ordinal);

    public static bool TryGetCurrentNovel(out StringDictionary translation)
    {
        translation = null;
        return IsTranslationEnabled() && TryGetLoadedNovel(out translation);
    }

    private static void EnsureCurrentNovelTranslationLoaded()
    {
        if (
            IsTranslationEnabled()
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

            if (!TryGetLoadedNovel(out var translation))
                return;

            var messageWindow = GetMessageWindow();
            if (messageWindow == null)
            {
                Logger.Info("Current novel message refresh skipped: no message window");
                return;
            }

            string current = messageWindow._messageData?.Message;
            if (string.IsNullOrEmpty(current))
                current = CurrentMessageForTranslationSetting();

            if (!string.IsNullOrEmpty(current))
                CaptureCurrentNovelMessages(translation, current);

            string selected = CurrentMessageForTranslationSetting();

            if (string.IsNullOrEmpty(selected) && messageWindow._messageData != null)
                selected = SelectNovelMessage(
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
        catch (System.Exception e)
        {
            Logger.Warn($"Current novel message refresh failed: {e.Message}");
        }
    }

    // ═══════════════════════════════════════
    //  剧情翻译 Harmony 补丁
    // ═══════════════════════════════════════

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelController), nameof(NovelController.InitNovel))]
    public static void InitNovelController(NovelController __instance)
    {
        _novelController = __instance;
        ResetCurrentMessageState();
    }

    /// <summary>
    /// 剧情目录解析后触发翻译加载。.Wait() 阻塞主线程是刻意为之 —
    /// 必须确保翻译数据在后续文本渲染前就绪。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelPathUtility), nameof(NovelPathUtility.GetNovelScenarioDirectory))]
    public static void SetupTranslation(string novelId)
    {
        if (!IsTranslationEnabled())
            return;

        Logger.Info($"NovelId: {novelId}");
        Plugin.Trans.GetNovelTranslationAsync(novelId).Wait();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelScriptInfoUtility), nameof(NovelScriptInfoUtility.GetScriptInfo))]
    public static void SetTitleAndDescription(ValueTuple<string, string> __result)
    {
        __result.Item1 = TranslateStaticForCurrentNovel(TitlesTable, __result.Item1);
        __result.Item2 = TranslateStaticForCurrentNovel(DescriptionsTable, __result.Item2);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelTitle), nameof(NovelTitle.SetTitle))]
    public static void SetTitle(ref string title)
    {
        title = TranslateStaticForCurrentNovel(TitlesTable, title);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelViewMessageWindow), nameof(NovelViewMessageWindow.SetName))]
    public static void SetName(ref string name)
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
    public static void SetText(
        Il2CppSystem.Collections.Generic.List<Letter> letters,
        ref string message
    )
    {
        if (TryGetLoadedNovel(out var translation))
        {
            CaptureCurrentNovelMessages(translation, message);
            message = CurrentMessageForTranslationSetting();
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
    public static bool SetLogAdd(
        string scriptId,
        string assetId,
        ref string charaName,
        ref string message,
        string logId,
        NovelSound voice,
        CancellationToken ct
    )
    {
        if (_refreshingCurrentMessage)
            return false;

        charaName = HideUserPlaceholder(charaName);
        message = HideUserPlaceholder(message);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelLogPopup), nameof(NovelLogPopup.SetData))]
    public static void SetLog(ref Il2CppSystem.Collections.Generic.List<NovelLogData> dataList)
    {
        var list = new Il2CppSystem.Collections.Generic.List<NovelLogData>();
        bool hasNovel = TryGetCurrentNovel(out var translation);

        foreach (var data in dataList)
        {
            string name = RestoreUserPlaceholder(data.Name);
            string message = RestoreUserPlaceholder(data.Message);

            if (hasNovel)
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
    public static void SetBalloon(CommandDotMessageData messageData)
    {
        messageData.Message = TranslateCurrentNovelText(messageData.Message);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(NovelCmdMessageTextCenter),
        nameof(NovelCmdMessageTextCenter.OnCommandStartASync)
    )]
    public static void SetTextCenter(NovelArguments args)
    {
        string message = args.GetString(2);
        if (ContainsUserPlaceholder(message))
            args._list[2] = NovelArgument.SetString(HideUserPlaceholder(message));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelModelMessageText), nameof(NovelModelMessageText.SetMessage))]
    public static void SetMessageText(CommandMessageTextData data)
    {
        data.Message = RestoreUserPlaceholder(data.Message);

        data.Message = TranslateCurrentNovelText(data.Message);

        if (ContainsUserPlaceholder(data.Message))
            data.Message = ExpandUserPlaceholder(
                data.Message,
                ReadDisplayUserName()
            );
    }
}
