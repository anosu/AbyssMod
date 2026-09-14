using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace AbyssMod.UI;

/// <summary>避开 Unity 6 IL2CPP 中还原不完整的 Font(string[], int) 构造函数。</summary>
internal sealed class SettingsMenuFont : IDisposable
{
    private readonly bool _owned;
    private bool _disposed;
    internal Font Asset { get; }

    private SettingsMenuFont(Font asset, bool owned)
    {
        Asset = asset;
        _owned = owned;
    }

    internal static SettingsMenuFont Load()
    {
        Exception lastError;
        try
        {
            var font = CreateNativeDynamicFont();
            LogSelection(font, "native dynamic CJK font");
            return new SettingsMenuFont(font, owned: true);
        }
        catch (Exception ex)
        {
            lastError = ex;
            Logger.Info(
                $"Settings menu native font unavailable; trying built-in fonts. {ex.GetType().Name}: {ex.Message}"
            );
        }

        foreach (string name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
        {
            try
            {
                var font = LoadBuiltinFont(name);
                LogSelection(font, "built-in " + name);
                return new SettingsMenuFont(font, owned: false);
            }
            catch (Exception ex)
            {
                lastError = ex;
                Logger.Info(
                    $"Settings menu font '{name}' unavailable: {ex.GetType().Name}: {ex.Message}"
                );
            }
        }

        throw new InvalidOperationException(
            "No compatible font is available for the settings menu.",
            lastError
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Font CreateNativeDynamicFont()
    {
        // Unity 的私有 Font(string[], int) 构造函数只调用 Internal_CreateDynamicFont。
        // 本游戏的桥接 DLL 缺少该构造函数，但保留了原生创建入口。先分配 IL2CPP 对象，
        // 再调用原生入口，避免 CreateDynamicFontFromOSFont 的损坏 MemberRef。
        IntPtr pointer = IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<Font>.NativeClassPtr);
        if (pointer == IntPtr.Zero)
            throw new InvalidOperationException("IL2CPP could not allocate a Font object.");

        var font = new Font(pointer);
        try
        {
            Font.Internal_CreateDynamicFont(
                font,
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Yu Gothic", "Arial" },
                20
            );
            if (font == null)
                throw new InvalidOperationException("Unity did not create a native Font asset.");
            font.hideFlags = HideFlags.HideAndDontSave;
            return font;
        }
        catch
        {
            if (font != null)
                UObject.Destroy(font);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Font LoadBuiltinFont(string name) =>
        Resources.GetBuiltinResource(Il2CppType.Of<Font>(), name)?.TryCast<Font>()
        ?? throw new InvalidOperationException($"Built-in font '{name}' was not found.");

    private static void LogSelection(Font font, string source)
    {
        Logger.Info($"Settings menu font ready: {source}.");
        try
        {
            Logger.Info($"Settings menu Chinese glyph available={font.HasCharacter('中')}.");
        }
        catch (Exception ex)
        {
            // 可选诊断失败不能阻止菜单创建。
            Logger.Info(
                $"Settings menu font glyph probe unavailable: {ex.GetType().Name}: {ex.Message}"
            );
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_owned && Asset != null)
            UObject.Destroy(Asset);
    }
}
