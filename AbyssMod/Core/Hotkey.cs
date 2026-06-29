using System.Collections.Generic;
using AbyssMod.Patches;
using AbyssMod.Services;
using BepInEx.Configuration;
using UnityEngine;

namespace AbyssMod;

/// <summary>
/// 快捷键处理。挂载为 MonoBehaviour，每帧检查按键输入。
/// 使用节流机制避免连续帧重复触发同一快捷键。
/// </summary>
public class Hotkey : MonoBehaviour
{
    private const float DebounceInterval = 0.15f;
    private readonly Dictionary<KeyCode, float> _lastPressTime = new();

    private void Update()
    {
        CheckToggle(KeyCode.F8, () => Config.Translation);
        CheckToggle(KeyCode.F9, () => Config.VoiceInterruption);

        if (Input.GetKeyDown(KeyCode.F10) && CanTrigger(KeyCode.F10))
        {
            Plugin.ConfigFile.Reload();
            Logger.Info("Config reloaded");
        }
    }

    private void CheckToggle(KeyCode key, System.Func<ConfigEntry<bool>> getter)
    {
        if (Input.GetKeyDown(key) && CanTrigger(key))
        {
            var entry = getter();
            entry.Value = !entry.Value;
        }
    }

    private bool CanTrigger(KeyCode key)
    {
        float now = Time.time;
        if (_lastPressTime.TryGetValue(key, out float last) && now - last < DebounceInterval)
            return false;
        _lastPressTime[key] = now;
        return true;
    }
}
