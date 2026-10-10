// Only Unity/host effects are replaced. Configuration tests use the real BepInEx ConfigFile.
using System;
using System.Collections.Generic;

namespace AbyssMod
{
    internal static class MyPluginInfo
    {
        public const string PLUGIN_GUID = "AbyssMod";
    }
}

namespace AbyssMod.Patches
{
    internal static class TranslationPatch
    {
        public static int RefreshCount;

        public static void RefreshCurrentMessage() => RefreshCount++;
    }
}

namespace AbyssMod.Services
{
    internal static class NovelStageVolumeController
    {
        public static void Update() { }
    }
}

namespace Utility.Notifications
{
    internal static class Toast
    {
        public static readonly List<string> Messages = new();

        public static void Info(string title, string message) => Messages.Add(title + message);

        public static void Warning(string title, string message) => Messages.Add(title + message);
    }
}

namespace Il2CppInterop.Runtime.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HideFromIl2CppAttribute : Attribute { }
}

namespace UnityEngine
{
    public class MonoBehaviour { }

    public class Transform
    {
        public Vector3 localScale;
    }

    public struct Vector3(float x, float y, float z)
    {
        public float x = x;
        public float y = y;
        public float z = z;
    }

    public struct Vector2(float x, float y)
    {
        public float x = x;
        public float y = y;
    }

    public enum KeyCode
    {
        F6,
        F8,
        F9,
        LeftControl,
        RightControl,
    }

    public static class Input
    {
        public static readonly HashSet<KeyCode> Pressed = new();
        public static Vector2 mouseScrollDelta;

        public static bool GetKey(KeyCode key) => Pressed.Contains(key);

        public static bool GetKeyDown(KeyCode key) => Pressed.Contains(key);
    }

    public static class Time
    {
        public static float unscaledTime;
    }

    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);

        public static float Round(float value) => MathF.Round(value);
    }
}
