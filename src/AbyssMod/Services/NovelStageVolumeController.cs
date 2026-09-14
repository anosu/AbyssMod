using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AbyssMod.Services;

/// <summary>
/// 只控制剧情 Live2D 特效对象上的 PostProcessVolume，保留舞台 IngameStageVolume 的原始行为。
/// 所有 Unity 对象访问都由插件生命周期和 Hotkey 在主线程上执行。
/// </summary>
internal static class NovelStageVolumeController
{
    private const string NovelName = "Novel";
    private const string NovelRootName = "NovelRoot";
    private const string VolumeRelativePath =
        "R18/R18Canvas/PosR18/RootR18/Live2DRoot/l2dObject/Live2DTag/EffectObject/PostProcessVolume";
    private const float DiscoveryInterval = 0.5f;

    private static readonly List<(Volume Volume, bool OriginalEnabled)> _volumes = new();
    private static bool _initialized;
    private static float _nextDiscoveryTime;

    internal static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        Reload();
    }

    internal static void Update()
    {
        if (!_initialized)
            return;

        // 定期重新查找，覆盖延迟创建、场景切换和同一场景内对象重建的情况。
        if (Time.unscaledTime >= _nextDiscoveryTime)
            DiscoverVolumes();
        Apply();
    }

    internal static void Toggle()
    {
        if (!_initialized)
            return;

        Config.NovelStageVolume.Value = !Config.NovelStageVolume.Value;
        Reload();
    }

    internal static void Reload()
    {
        if (!_initialized)
            return;

        DiscoverVolumes();
        Apply();
    }

    private static void DiscoverVolumes()
    {
        _nextDiscoveryTime = Time.unscaledTime + DiscoveryInterval;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            // GetRootGameObjects + Transform.Find 也能找到未激活的层级。
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                // 兼容 Novel 为场景名或根 GameObject 名的两种路径表示。
                var target = root.name switch
                {
                    NovelName => root.transform.Find(NovelRootName + "/" + VolumeRelativePath),
                    NovelRootName when scene.name == NovelName => root.transform.Find(
                        VolumeRelativePath
                    ),
                    _ => null,
                };
                if (target == null)
                    continue;

                var volume = target.GetComponent<Volume>();
                if (volume == null || _volumes.Exists(entry => entry.Volume == volume))
                    continue;

                _volumes.Add((volume, volume.enabled));
            }
        }
    }

    private static void Apply()
    {
        bool enabled = Config.NovelStageVolume.Value;
        for (int i = _volumes.Count - 1; i >= 0; i--)
        {
            var volume = _volumes[i].Volume;
            if (volume == null)
            {
                _volumes.RemoveAt(i);
                continue;
            }

            if (volume.enabled != enabled)
                volume.enabled = enabled;
        }
    }

    internal static void Shutdown()
    {
        _initialized = false;
        foreach (var (volume, originalEnabled) in _volumes)
        {
            if (volume != null)
                volume.enabled = originalEnabled;
        }
        _volumes.Clear();
    }
}
