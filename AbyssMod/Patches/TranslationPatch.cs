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

namespace AbyssMod.Patches;

using NovelLogList = Il2CppSystem.Collections.Generic.List<NovelLogData>;
using TranslationTable = Dictionary<string, string>;

/// <summary>
/// 剧情翻译补丁：覆盖剧情人名、对话及日志，独立于 UI 翻译开关。
/// </summary>
[HarmonyPatch]
public static class TranslationPatch
{
    private const string UserPlaceholder = "<user>";
    private const string HiddenUserPlaceholder = "%user%";

    private static NovelController _novelController;
    private static NovelViewMessageWindow _messageWindow;
    private static string _currentOriginalMessage;
    private static string _currentTranslatedMessage;
    private static bool _refreshingCurrentMessage;
    private static string NovelId => _novelController?._common?.ScriptId ?? string.Empty;

    private static bool CanTranslate() => Config.Translation.Value && Plugin.Trans != null;

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

    private static string TranslateCurrentNovelText(string sourceText) =>
        CanTranslate() && TryGetNovel(NovelId, out var translation)
            ? TranslateFrom(translation, sourceText)
            : sourceText;

    private static bool TryGetNovel(string novelId, out TranslationTable translation)
    {
        translation = null;
        return Plugin.Trans != null
            && !string.IsNullOrEmpty(novelId)
            && Plugin.Trans.TryGetNovel(novelId, out translation);
    }

    private static string TranslateNovelMessage(
        TranslationTable translation,
        string sourceText,
        string displayName
    )
    {
        if (string.IsNullOrEmpty(sourceText) || translation == null)
            return sourceText;

        return
            translation.TryGetValue(sourceText, out string translated)
            && !string.IsNullOrEmpty(translated)
            ? ExpandUserPlaceholder(translated, displayName)
            : sourceText;
    }

    private static void CaptureCurrentNovelMessages(TranslationTable translation, string message)
    {
        string displayName = GetDisplayUserName();
        _currentOriginalMessage = message;
        _currentTranslatedMessage = TranslateNovelMessage(translation, message, displayName);
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
        if (!CanTranslate())
            return;

        Plugin.Trans.EnsureStaticTranslationsLoadedAsync().Wait();
        if (!string.IsNullOrEmpty(NovelId) && !Plugin.Trans.HasNovel(NovelId))
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

            if (string.IsNullOrEmpty(_currentOriginalMessage))
            {
                string current = messageWindow._messageData?.Message;
                if (!string.IsNullOrEmpty(current))
                    CaptureCurrentNovelMessages(translation, current);
            }

            if (Config.Translation.Value && !string.IsNullOrEmpty(_currentOriginalMessage))
                _currentTranslatedMessage = TranslateNovelMessage(
                    translation,
                    _currentOriginalMessage,
                    GetDisplayUserName()
                );

            string selected = GetConfiguredCurrentMessage();

            if (string.IsNullOrEmpty(selected) && messageWindow._messageData != null)
                selected = Config.Translation.Value
                    ? TranslateNovelMessage(
                        translation,
                        messageWindow._messageData.Message,
                        GetDisplayUserName()
                    )
                    : messageWindow._messageData.Message;

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
        Plugin.Trans.EnsureStaticTranslationsLoadedAsync().Wait();
        Plugin.Trans.GetNovelTranslationAsync(novelId).Wait();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelViewMessageWindow), nameof(NovelViewMessageWindow.SetName))]
    public static void TranslateSpeakerName(ref string name)
    {
        if (CanTranslate() && TryGetNovel(NovelId, out _))
            name = Plugin.Trans.TranslateName(name);
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
        if (_refreshingCurrentMessage)
            return;

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
                name = Plugin.Trans.TranslateName(name);
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
