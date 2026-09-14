using System.Collections.Generic;
using Project.Novel;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace AbyssMod.UI;

/// <summary>
/// 暂时暂停游戏的剧情输入组件，不禁用菜单自己的 ScrollRect / InputField。
/// 不要再给 NovelInputComponent 的长按检查方法加 Harmony 补丁：这些 IL2CPP 热路径
/// 通过原生委托尾调用，运行原方法的 Harmony 跳板会在持续按压时使游戏崩溃。
/// </summary>
internal static class MenuGameInputGuard
{
    private static readonly List<(NovelInputComponent Input, bool WasInteractable)> _inputs = new();
    private static float _nextScanTime;

    internal static void Update(bool forceScan = false)
    {
        if (!MenuMouseIsolation.BlocksGame)
        {
            Restore();
            return;
        }

        if (forceScan || Time.unscaledTime >= _nextScanTime)
        {
            _nextScanTime = Time.unscaledTime + 0.25f;
            foreach (
                var input in UObject.FindObjectsByType<NovelInputComponent>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
            {
                if (input != null && !_inputs.Exists(entry => entry.Input == input))
                    _inputs.Add((input, input._isInteractable));
            }
        }

        for (int i = _inputs.Count - 1; i >= 0; i--)
        {
            var input = _inputs[i].Input;
            if (input == null)
            {
                _inputs.RemoveAt(i);
                continue;
            }
            input._isInteractable = false;
            CancelHold(input);
        }
    }

    internal static void CancelHold(NovelInputComponent input)
    {
        if (input._downCoroutine != null)
        {
            input.StopDownCoroutine();
            input._downCoroutine = null;
        }
        // 不能调用 Clear()：它还会清掉游戏注册的输入回调。
        input._isClick = false;
        input._downElapse = 0;
        input._repeatElapse = 0;
        input._pressState = NovelInputComponent.State.Wait;
        input._repeatState = NovelInputComponent.State.Wait;
    }

    internal static void Restore()
    {
        foreach (var (input, interactable) in _inputs)
        {
            if (input == null)
                continue;
            CancelHold(input);
            input._isInteractable = interactable;
        }
        _inputs.Clear();
        _nextScanTime = 0;
    }
}
