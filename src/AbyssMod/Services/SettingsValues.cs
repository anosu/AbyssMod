using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;

namespace AbyssMod.Services;

// 菜单只编辑这些用户设置；不改动解密密钥或调试接口等隐藏配置。
internal sealed record SettingsValues
{
    public bool Translation { get; init; }
    public bool UiTranslation { get; init; }
    public bool DynamicMosaic { get; init; }
    public bool SoundCaution { get; init; }
    public bool VoiceInterruption { get; init; }
    public bool TitleMovie { get; init; }
    public float Live2DScale { get; init; }
    public bool Live2DEffects { get; init; }
    public bool PreferLocalFiles { get; init; }
    public bool LegacyHotkeys { get; init; }
    public string Cdn { get; init; }
    public string Language { get; init; }
    public string CacheDirectory { get; init; }
    public string FontBundlePath { get; init; }

    public static SettingsValues Read(bool defaults = false) =>
        new()
        {
            Translation = Value(Config.Translation, defaults),
            UiTranslation = Value(Config.UiTranslation, defaults),
            DynamicMosaic = Value(Config.DynamicMosaic, defaults),
            SoundCaution = Value(Config.SoundCaution, defaults),
            VoiceInterruption = Value(Config.VoiceInterruption, defaults),
            TitleMovie = Value(Config.TitleMovie, defaults),
            Live2DScale = Value(Config.NovelLive2DScale, defaults),
            Live2DEffects = Value(Config.NovelStageVolume, defaults),
            PreferLocalFiles = Value(Config.TranslationPreferLocalFiles, defaults),
            LegacyHotkeys = Value(Config.LegacyHotkeys, defaults),
            Cdn = Value(Config.TranslationCDN, defaults),
            Language = Value(Config.TranslationLanguage, defaults),
            CacheDirectory = Value(Config.TranslationCacheDirectory, defaults),
            FontBundlePath = Value(Config.FontBundlePath, defaults),
        };

    public SettingsValues Normalize() =>
        this with
        {
            Cdn = Cdn?.Trim(),
            Language = Language?.Trim(),
            CacheDirectory = CacheDirectory?.Trim(),
            FontBundlePath = FontBundlePath?.Trim(),
        };

    public string Validate()
    {
        if (!float.IsFinite(Live2DScale) || Live2DScale < 0.1f || Live2DScale > 10f)
            return "H场景尺寸大小应在 10% 至 1000% 之间。";
        if (
            !Uri.TryCreate(Cdn, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http")
        )
            return "翻译源必须是以 https:// 或 http:// 开头的完整网址。";
        if (string.IsNullOrWhiteSpace(Language) || !Regex.IsMatch(Language, "^[A-Za-z0-9_-]+$"))
            return "语言代码只能包含字母、数字、下划线或连字符，例如 zh_Hans。";
        if (string.IsNullOrWhiteSpace(CacheDirectory))
            return "翻译缓存目录不能为空。";
        if (string.IsNullOrWhiteSpace(FontBundlePath))
            return "字体资源路径不能为空。";
        return null;
    }

    internal void Write()
    {
        Set(Config.Translation, Translation);
        Set(Config.UiTranslation, UiTranslation);
        Set(Config.DynamicMosaic, DynamicMosaic);
        Set(Config.SoundCaution, SoundCaution);
        Set(Config.VoiceInterruption, VoiceInterruption);
        Set(Config.TitleMovie, TitleMovie);
        Set(Config.NovelLive2DScale, Live2DScale);
        Set(Config.NovelStageVolume, Live2DEffects);
        Set(Config.TranslationPreferLocalFiles, PreferLocalFiles);
        Set(Config.LegacyHotkeys, LegacyHotkeys);
        Set(Config.TranslationCDN, Cdn);
        Set(Config.TranslationLanguage, Language);
        Set(Config.TranslationCacheDirectory, CacheDirectory);
        Set(Config.FontBundlePath, FontBundlePath);
    }

    private static T Value<T>(ConfigEntry<T> entry, bool defaults) =>
        defaults ? (T)entry.DefaultValue : entry.Value;

    private static void Set<T>(ConfigEntry<T> entry, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(entry.Value, value))
            entry.Value = value;
    }
}
