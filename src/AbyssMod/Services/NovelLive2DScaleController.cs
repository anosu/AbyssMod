using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace AbyssMod.Services;

/// <summary>保存 Live2D 原始尺寸，处理配置刷新和滚轮缩放，退出时恢复场景。</summary>
internal static class NovelLive2DScaleController
{
    private const float SaveDelay = 1f;
    private static readonly Dictionary<Transform, Vector3> _originalScales = new();
    private static float _scale = float.NaN;
    private static float _saveTime;
    private static bool _savePending;

    internal static void Track(Transform root)
    {
        if (root == null)
            return;

        if (!_originalScales.TryGetValue(root, out var original))
        {
            original = root.localScale;
            _originalScales.Add(root, original);
        }
        if (float.IsNaN(_scale))
            _scale = Config.NovelLive2DScale.Value;
        root.localScale = Scale(original);
    }

    internal static void Release(Transform root)
    {
        if (
            !ReferenceEquals(root, null)
            && _originalScales.Remove(root, out var original)
            && root != null
        )
            root.localScale = original;
    }

    internal static void Update()
    {
        bool controlPressed =
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (_originalScales.Count == 0 || !controlPressed || Input.mouseScrollDelta.y == 0)
        {
            if (_savePending && Time.unscaledTime >= _saveTime)
                Save();
            return;
        }

        var range =
            (AcceptableValueRange<float>)Config.NovelLive2DScale.Description.AcceptableValues;
        float next = Mathf.Clamp(
            Mathf.Round((_scale + Input.mouseScrollDelta.y * 0.01f) * 100f) / 100f,
            range.MinValue,
            range.MaxValue
        );
        if (next == _scale)
            return;

        _scale = next;
        _saveTime = Time.unscaledTime + SaveDelay;
        _savePending = true;
        Apply();
    }

    internal static void Reload()
    {
        // 外部配置修改优先于尚未写回的滚轮值。
        _savePending = false;
        _scale = Config.NovelLive2DScale.Value;
        Apply();
    }

    private static void Apply()
    {
        List<Transform> stale = null;
        foreach (var (root, original) in _originalScales)
        {
            if (root == null)
                (stale ??= new List<Transform>()).Add(root);
            else
                root.localScale = Scale(original);
        }
        if (stale != null)
            foreach (var root in stale)
                _originalScales.Remove(root);
    }

    private static Vector3 Scale(Vector3 original) =>
        new(original.x * _scale, original.y * _scale, original.z);

    private static void Save()
    {
        _savePending = false;
        Config.NovelLive2DScale.Value = _scale;
    }

    internal static void Shutdown()
    {
        if (_savePending)
            Save();
        foreach (var (root, original) in _originalScales)
            if (root != null)
                root.localScale = original;
        _originalScales.Clear();
        _scale = float.NaN;
    }
}
