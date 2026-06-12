using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Project.Novel;

namespace AbyssMod.Patches;

/// <summary>
/// 剧情翻译补丁：标题、人名、对话文本的翻译注入。
/// </summary>
[HarmonyPatch]
public static class TranslationPatch
{
    private static TopScene _topScene;
    private static string NovelId
    {
        get => _topScene?._param?.novelId ?? string.Empty;
    }

    public static bool TryGetCurrentNovel(
        out System.Collections.Generic.Dictionary<string, string> translation
    )
    {
        translation = null;
        return Config.Translation.Value
            && Plugin.Trans.Novels.TryGetValue(NovelId, out translation);
    }

    /// <summary>
    /// 剧情加载时获取对应 Novel ID，触发翻译数据预加载。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelPathUtility), nameof(NovelPathUtility.GetNovelScenarioDirectory))]
    public static void SetupTranslation(string novelId)
    {
        if (!Config.Translation.Value)
            return;

        Plugin.Log.LogInfo($"NovelId: {novelId}");

        Plugin.Trans.GetNovelTranslationAsync(novelId).Wait();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelScriptInfoUtility), nameof(NovelScriptInfoUtility.GetScriptInfo))]
    public static void SetTitleAndDescription(ValueTuple<string, string> __result)
    {
        if (TryGetCurrentNovel(out var _))
        {
            string title = __result.Item1;
            if (title != null && Plugin.Trans.Titles.TryGetValue(title, out string text))
                __result.Item1 = text;

            string description = __result.Item2;
            if (
                description != null
                && Plugin.Trans.Descriptions.TryGetValue(description, out string desc)
            )
                __result.Item2 = desc;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelTitle), nameof(NovelTitle.SetTitle))]
    public static void SetTitle(ref string title)
    {
        if (TryGetCurrentNovel(out var _))
        {
            if (title != null && Plugin.Trans.Titles.TryGetValue(title, out string text))
                title = text;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelViewMessageWindow), nameof(NovelViewMessageWindow.SetName))]
    public static void SetName(ref string name)
    {
        if (TryGetCurrentNovel(out var _))
        {
            if (name != null && Plugin.Trans.Names.TryGetValue(name, out string text))
                name = text;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelText), nameof(NovelText.Parse))]
    public static void SetText(List<Letter> letters, ref string message)
    {
        if (TryGetCurrentNovel(out var translation))
        {
            if (message != null && translation.TryGetValue(message, out string text))
                message = text;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelLogPopup), nameof(NovelLogPopup.SetData))]
    public static void SetLog(List<NovelLogData> dataList)
    {
        if (TryGetCurrentNovel(out var translation))
        {
            foreach (var data in dataList)
            {
                if (data.Name != null && Plugin.Trans.Names.TryGetValue(data.Name, out string name))
                    data.Name = name;

                if (data.Message != null && translation.TryGetValue(data.Message, out string text))
                    data.Message = text;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TopScene), nameof(TopScene.CreateNovelProcessCallBacks))]
    public static void SetNovelProcessCallback(TopScene __instance)
    {
        _topScene = __instance;
    }
}
