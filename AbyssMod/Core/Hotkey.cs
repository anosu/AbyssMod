using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace AbyssMod;

/// <summary>
/// 快捷键处理（MonoBehaviour）。F8 切换翻译、F9 切换语音中断、F10 重载配置。
/// </summary>
public class Hotkey : MonoBehaviour
{
    private const float DebounceInterval = 0.15f;
    private readonly Dictionary<KeyCode, float> _lastPressTime = new();

    private void Update()
    {
        CheckToggle(KeyCode.F8, Config.Translation);
        CheckToggle(KeyCode.F9, Config.VoiceInterruption);

        if (Input.GetKeyDown(KeyCode.F10) && CanTrigger(KeyCode.F10))
        {
            Plugin.ConfigFile.Reload();
            Logger.Info("Config reloaded");
        }
    }

    private void CheckToggle(KeyCode key, ConfigEntry<bool> entry)
    {
        if (Input.GetKeyDown(key) && CanTrigger(key))
            entry.Value = !entry.Value;
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
