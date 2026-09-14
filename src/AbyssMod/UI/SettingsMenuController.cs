using System;
using AbyssMod.Patches;
using AbyssMod.Services;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.EventSystems;
using Utility.Notifications;

namespace AbyssMod.UI;

internal static class SettingsMenuController
{
    private static Transform _host;
    private static SettingsSession _session;
    private static SettingsMenuView _view;
    private static bool _initialized;
    private static bool _creationFailed;
    private static bool _oldCursorVisible;
    private static CursorLockMode _oldCursorLock;
    private static GameObject _oldSelection;

    internal static bool IsMenuTarget(GameObject target) =>
        MenuMouseIsolation.IsOpen && _view?.Contains(target) == true;

    internal static void Initialize(Transform host)
    {
        if (_initialized)
            return;
        _host = host;
        _session = new SettingsSession();
        _initialized = true;
        _creationFailed = false;
        MenuMouseIsolation.Reset();
    }

    private static void TryCreateView()
    {
        if (EventSystem.current == null || _host == null)
            return;
        try
        {
            _view = new SettingsMenuView(_host, _session, RequestClose, DiscardAndClose);
            _creationFailed = false;
            Logger.Info("In-game MOD settings menu ready. Use F10 to open it.");
        }
        catch (Exception ex)
        {
            _creationFailed = true;
            Logger.Error($"Settings menu creation failed: {ex}");
        }
    }

    internal static void Toggle()
    {
        if (!_initialized)
            return;
        if (MenuMouseIsolation.IsOpen)
        {
            RequestClose();
            return;
        }
        if (_view == null)
            TryCreateView();
        if (_view == null)
        {
            Toast.Info(
                "MOD 设置",
                _creationFailed
                    ? "菜单创建失败，详情见 BepInEx/LogOutput.log。"
                    : "游戏界面尚未就绪，请稍后再按 F10。"
            );
            return;
        }

        EnhancePatch.FlushNovelLive2DScale();
        _session.Begin();
        _oldCursorVisible = Cursor.visible;
        _oldCursorLock = Cursor.lockState;
        _oldSelection = EventSystem.current?.currentSelectedGameObject;
        MenuMouseIsolation.Begin();
        ClearPointerCapture();
        MenuGameInputGuard.Update(forceScan: true);
        EventSystem.current?.SetSelectedGameObject(null);
        _view.SetOpen(true);
        MaintainCursor();
    }

    internal static void RequestClose()
    {
        if (!MenuMouseIsolation.IsOpen)
            return;
        if (_session.HasChanges)
        {
            _session.WarnUnsaved();
            _view.ShowStatus(_session.Status, warning: true);
            return;
        }
        Close();
    }

    private static void DiscardAndClose()
    {
        _session.Begin();
        Close();
    }

    private static void Close()
    {
        if (!MenuMouseIsolation.IsOpen)
            return;
        MenuMouseIsolation.End();
        ClearPointerCapture();
        _view?.SetOpen(false);
        Cursor.lockState = _oldCursorLock;
        Cursor.visible = _oldCursorVisible;
        var system = EventSystem.current;
        if (system != null)
            system.SetSelectedGameObject(
                _oldSelection != null && _oldSelection.activeInHierarchy ? _oldSelection : null
            );
        _oldSelection = null;
    }

    internal static void MaintainCursor()
    {
        if (!MenuMouseIsolation.IsOpen)
            return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void ClearPointerCapture()
    {
        // 清掉旧界面的按下/拖拽捕获，避免抬键被发送给打开菜单前的游戏对象。
        EventSystem.current?.currentInputModule?.TryCast<PointerInputModule>()?.ClearSelection();
    }

    internal static void Shutdown()
    {
        Close();
        _initialized = false;
        _view?.Dispose();
        _view = null;
        _host = null;
        _session = null;
        MenuMouseIsolation.Reset();
        MenuGameInputGuard.Restore();
    }
}
