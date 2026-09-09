using System;
using AbyssMod.Services;
using HarmonyLib;
using Project.Novel;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssMod.Patches;

/// <summary>把即将显示的游戏 Sprite 替换为 manifest 指定的本地图片。</summary>
[HarmonyPatch]
public static class ImageReplacementPatch
{
    private static bool _errorLogged;

    [HarmonyPrefix, HarmonyPatch(typeof(Image), "set_sprite")]
    public static void ReplaceImageSprite(Image __instance, ref Sprite value)
    {
        value = ReplaceUiImageSafely(__instance, value);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Image), "set_overrideSprite")]
    public static void ReplaceImageOverrideSprite(Image __instance, ref Sprite value)
    {
        value = ReplaceUiImageSafely(__instance, value);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Image), "OnEnable")]
    public static void ReplaceEnabledImage(Image __instance)
    {
        if (__instance == null)
            return;

        Sprite source = __instance.sprite;
        Sprite replacement = ReplaceUiImageSafely(__instance, source);
        if (replacement != source)
            __instance.sprite = replacement;

        source = __instance.overrideSprite;
        if (source == null)
            return;
        replacement = ReplaceUiImageSafely(__instance, source);
        if (replacement != source)
            __instance.overrideSprite = replacement;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(SpriteRenderer), "set_sprite")]
    public static void ReplaceSpriteRenderer(ref Sprite value)
    {
        value = ReplaceBySpriteNameSafely(value);
    }

    [
        HarmonyPrefix,
        HarmonyPatch(typeof(NovelModelBG), nameof(NovelModelBG.SetBG), typeof(string), typeof(Sprite))
    ]
    public static void ReplaceNovelBackground(string id, ref Sprite sprite)
    {
        sprite = ReplaceNovelBackgroundSafely(id, sprite);
    }

    [
        HarmonyPrefix,
        HarmonyPatch(
            typeof(NovelSubImageComponent),
            nameof(NovelSubImageComponent.SetSprite),
            typeof(Sprite),
            typeof(float)
        )
    ]
    public static void ReplaceNovelSubImage(ref Sprite sprite)
    {
        sprite = ReplaceBySpriteNameSafely(sprite);
    }

    private static Sprite ReplaceUiImageSafely(Image image, Sprite original)
    {
        try
        {
            return Plugin.Images?.ReplaceUiImage(image, original) ?? original;
        }
        catch (Exception e)
        {
            LogHookError(e);
            return original;
        }
    }

    private static Sprite ReplaceBySpriteNameSafely(Sprite original)
    {
        try
        {
            return Plugin.Images?.ReplaceBySpriteName(original) ?? original;
        }
        catch (Exception e)
        {
            LogHookError(e);
            return original;
        }
    }

    private static Sprite ReplaceNovelBackgroundSafely(string id, Sprite original)
    {
        try
        {
            return Plugin.Images?.ReplaceNovelBackground(id, original) ?? original;
        }
        catch (Exception e)
        {
            LogHookError(e);
            return original;
        }
    }

    private static void LogHookError(Exception e)
    {
        if (_errorLogged)
            return;

        _errorLogged = true;
        Logger.Warn($"Image replacement failed; further hook errors suppressed: {e.Message}");
    }
}
