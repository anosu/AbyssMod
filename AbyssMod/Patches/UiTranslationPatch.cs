using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AbyssMod.Patches;

/// <summary>翻译启动时创建及后续动态生成的 TMP UI 文本。</summary>
[HarmonyPatch]
public static class UiTranslationPatch
{
    private static bool _errorLogged;

    private static string Translate(TMP_Text text, string source)
    {
        if (Plugin.Trans == null || string.IsNullOrEmpty(source) || !Plugin.Trans.HasUiTranslations)
            return source;

        return Plugin.Trans.TranslateUiText(GetTransformPath(text?.transform), source);
    }

    private static string TranslateSafely(TMP_Text text, string source)
    {
        try
        {
            return Translate(text, source);
        }
        catch (Exception e)
        {
            if (!_errorLogged)
            {
                _errorLogged = true;
                Logger.Warn($"UI text translation failed; further errors suppressed: {e.Message}");
            }
            return source;
        }
    }

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
            return null;

        var parts = new Stack<string>();
        for (var current = transform; current != null; current = current.parent)
            parts.Push(current.name);
        return string.Join("/", parts);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TMP_Text), "set_text")]
    public static void TranslateTextSetter(TMP_Text __instance, ref string value) =>
        value = TranslateSafely(__instance, value);

    [HarmonyPrefix, HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string))]
    public static void TranslateSetText(TMP_Text __instance, ref string sourceText) =>
        sourceText = TranslateSafely(__instance, sourceText);

    [
        HarmonyPrefix,
        HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(bool))
    ]
    public static bool TranslateSetTextAndSyncInputBox(TMP_Text __instance, ref string sourceText)
    {
        __instance.text = TranslateSafely(__instance, sourceText);
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

        string translated = TranslateSafely(text, text.text);
        if (!string.Equals(translated, text.text, StringComparison.Ordinal))
            text.text = translated;
    }
}
