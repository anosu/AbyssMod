using System;
using System.Collections.Generic;
using System.Globalization;
using AbyssMod.Services;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Utility.Notifications;
using UObject = UnityEngine.Object;

namespace AbyssMod.UI;

/// <summary>独立的 uGUI 菜单，不依赖游戏翻译字体或 UnityExplorer。</summary>
internal sealed class SettingsMenuView : IDisposable
{
    private static readonly Color Background = new(0.075f, 0.09f, 0.13f, 1f);
    private static readonly Color Surface = new(0.13f, 0.155f, 0.205f, 1f);
    private static readonly Color Foreground = new(0.94f, 0.96f, 1f, 1f);
    private static readonly Color Muted = new(0.66f, 0.72f, 0.80f, 1f);
    private static readonly Color Accent = new(0.065f, 0.43f, 0.405f, 1f);
    private static readonly Color Warning = new(1f, 0.77f, 0.40f, 1f);
    private readonly SettingsSession _session;
    private readonly SettingsMenuFont _fontLease;
    private readonly Font _font;
    private readonly GameObject _root;
    private readonly GameObject _modal;
    private readonly ScrollRect _scroll;
    private readonly RectTransform _viewport;
    private readonly Text _status;
    private readonly Text _closeLabel;
    private readonly Button _save;
    private readonly Button _reload;
    private readonly List<UnityAction> _permanentListeners = new();
    private readonly List<Il2CppSystem.Delegate> _pageListeners = new();
    private readonly List<Action> _refreshers = new();
    private readonly List<Image> _tabs = new();
    private RectTransform _content;
    private int _page;
    private float _rowY;
    private bool _disposed;
    private string _feedback;
    private bool _feedbackWarning;

    internal SettingsMenuView(
        Transform host,
        SettingsSession session,
        Action requestClose,
        Action discardClose
    )
    {
        _session = session;
        _fontLease = SettingsMenuFont.Load();
        _font = _fontLease.Asset;
        try
        {
            _root = Create(
                host,
                "AbyssMod.Settings",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32750;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _modal = Create(_root.transform, "Modal", typeof(Image));
            Stretch(_modal.GetComponent<RectTransform>());
            _modal.GetComponent<Image>().color = new Color(0, 0, 0, 0.64f);
            // 整屏 Image 与 GraphicRaycaster 拦住菜单区域和遮罩上的游戏 UI 点击。
            var panel = Create(_modal.transform, "Panel", typeof(Image));
            panel.GetComponent<Image>().color = Background;
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(940, 650);
            // 窗口和内部文字、控件统一缩小 10%，整屏点击遮罩不缩放。
            panelRect.localScale = new Vector3(0.9f, 0.9f, 1f);

            Label(panel.transform, "MOD 设置", 28, 20, 740, 34, 28, Foreground, bold: true);
            Label(
                panel.transform,
                "AbyssMod  ·  不用修改配置文件  ·  F10 打开菜单",
                28,
                61,
                790,
                25,
                16,
                Muted
            );
            MakeButton(panel.transform, "关闭", (832, 22, 80, 36), requestClose, permanent: true);

            string[] pages = { "翻译设置", "画面与声音", "高级选项" };
            for (int i = 0; i < pages.Length; i++)
            {
                int page = i;
                var tab = MakeButton(
                    panel.transform,
                    pages[i],
                    (28 + i * 298, 99, 286, 39),
                    () =>
                    {
                        _page = page;
                        BuildPage();
                    },
                    permanent: true
                );
                _tabs.Add(tab.Background);
            }

            var scrollObject = Create(
                panel.transform,
                "Options",
                typeof(Image),
                typeof(ScrollRect)
            );
            Top(scrollObject.GetComponent<RectTransform>(), 28, 153, 884, 383);
            scrollObject.GetComponent<Image>().color = new Color(0.085f, 0.10f, 0.145f, 1);
            _scroll = scrollObject.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30;
            var viewportObject = Create(scrollObject.transform, "Viewport", typeof(RectMask2D));
            _viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(_viewport);
            _viewport.offsetMin = new Vector2(12, 8);
            _viewport.offsetMax = new Vector2(-30, -8);
            _scroll.viewport = _viewport;

            var bar = Create(scrollObject.transform, "Scrollbar", typeof(Image), typeof(Scrollbar));
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1, 0);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(1, 0.5f);
            barRect.sizeDelta = new Vector2(12, -16);
            barRect.anchoredPosition = new Vector2(-7, 0);
            bar.GetComponent<Image>().color = Surface;
            var sliding = Create(bar.transform, "SlidingArea");
            Stretch(sliding.GetComponent<RectTransform>());
            var handle = Create(sliding.transform, "Handle", typeof(Image));
            Stretch(handle.GetComponent<RectTransform>());
            handle.GetComponent<Image>().color = Muted;
            var scrollbar = bar.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            _status = Label(panel.transform, "", 28, 544, 884, 38, 16, Muted);
            MakeButton(
                panel.transform,
                "恢复默认",
                (28, 588, 128, 40),
                () =>
                {
                    _session.RestoreDefaults();
                    _feedback = null;
                    Refresh();
                },
                permanent: true
            );
            _reload = MakeButton(
                panel.transform,
                "从文件重读",
                (168, 588, 150, 40),
                () =>
                {
                    if (_session.HasChanges)
                        return;
                    bool success = _session.ReloadFromFile();
                    ShowStatus(_session.Status, !success);
                    Refresh();
                },
                permanent: true
            ).Button;
            _closeLabel = MakeButton(
                panel.transform,
                "关闭",
                (582, 588, 146, 40),
                discardClose,
                permanent: true
            ).Label;
            var save = MakeButton(
                panel.transform,
                "保存并应用",
                (744, 588, 168, 40),
                () =>
                {
                    bool success = _session.Apply();
                    ShowStatus(_session.Status, !success || _session.RestartRequired);
                    Refresh();
                    if (success)
                        Toast.Info(
                            "MOD 设置",
                            _session.RestartRequired
                                ? "已保存，部分设置需重启游戏生效。"
                                : "已保存并应用。"
                        );
                },
                permanent: true
            );
            _save = save.Button;
            save.Background.color = Accent;

            BuildPage();
            _modal.SetActive(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void SetOpen(bool open)
    {
        _feedback = null;
        _modal.SetActive(open);
        if (open)
            Refresh();
    }

    internal bool Contains(GameObject target) =>
        target != null && _root != null && target.transform.IsChildOf(_root.transform);

    internal void ShowStatus(string text, bool warning)
    {
        _feedback = text;
        _feedbackWarning = warning;
        RefreshSummary();
    }

    private void BuildPage()
    {
        if (_content != null)
        {
            _content.gameObject.SetActive(false);
            UObject.Destroy(_content.gameObject);
        }
        _pageListeners.Clear();
        _refreshers.Clear();
        _content = Create(_viewport, "Content").GetComponent<RectTransform>();
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = new Vector2(1, 1);
        _content.pivot = new Vector2(0, 1);
        _content.anchoredPosition = Vector2.zero;
        _rowY = 0;
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].color = i == _page ? Accent : Surface;

        if (_page == 0)
        {
            ToggleRow(
                "剧情翻译",
                "剧情文字立即生效；资料（MasterData）翻译需重启游戏。",
                s => s.Translation,
                (s, v) => s with { Translation = v }
            );
            ToggleRow(
                "界面与图片翻译",
                "需要重启游戏。包括界面文字、按钮图片和背景替换。",
                s => s.UiTranslation,
                (s, v) => s with { UiTranslation = v }
            );
            ToggleRow(
                "优先使用本地翻译",
                "需要重启游戏。已有本地译文优先于远程更新。",
                s => s.PreferLocalFiles,
                (s, v) => s with { PreferLocalFiles = v }
            );
            FieldRow("翻译源网址（CDN） · 需重启", s => s.Cdn, (s, v) => s with { Cdn = v });
            FieldRow(
                "翻译语言代码 · 需重启（简体中文：zh_Hans）",
                s => s.Language,
                (s, v) => s with { Language = v }
            );
            FieldRow(
                "翻译缓存目录 · 需重启",
                s => s.CacheDirectory,
                (s, v) => s with { CacheDirectory = v }
            );
            FieldRow(
                "字体资源路径 · 需重启",
                s => s.FontBundlePath,
                (s, v) => s with { FontBundlePath = v }
            );
        }
        else if (_page == 1)
        {
            ToggleRow(
                "H场景滤镜",
                "立即生效。控制附加泛光、色差；保留舞台基础效果。",
                s => s.Live2DEffects,
                (s, v) => s with { Live2DEffects = v }
            );
            ScaleRow();
            ToggleRow(
                "不中断角色语音",
                "立即生效。下一句没有语音时，继续播放当前语音。",
                s => !s.VoiceInterruption,
                (s, v) => s with { VoiceInterruption = !v }
            );
            ToggleRow(
                "动态马赛克",
                "下次载入角色生效。修改后请重新进入剧情。",
                s => s.DynamicMosaic,
                (s, v) => s with { DynamicMosaic = v }
            );
            ToggleRow(
                "播放标题动画",
                "下次进入标题界面时生效。",
                s => s.TitleMovie,
                (s, v) => s with { TitleMovie = v }
            );
            ToggleRow(
                "进入游戏时显示音量提醒",
                "下次出现该提醒时生效。",
                s => s.SoundCaution,
                (s, v) => s with { SoundCaution = v }
            );
        }
        else
        {
            ToggleRow(
                "启用兼容快捷键",
                "",
                s => s.LegacyHotkeys,
                (s, v) => s with { LegacyHotkeys = v }
            );
            Label(_content, "快捷键说明", 4, _rowY + 8, 824, 28, 20, Foreground, bold: true);
            _rowY += 40;
            ShortcutRow("F10", "打开 / 关闭 MOD 设置菜单");
            ShortcutRow("Esc", "关闭菜单；有未保存修改时先提示");
            ShortcutRow("F6", "切换H场景滤镜（需开启兼容快捷键）");
            ShortcutRow("F8", "切换剧情翻译（需开启兼容快捷键）");
            ShortcutRow("F9", "切换语音是否被打断（需开启兼容快捷键）");
            ShortcutRow("Ctrl + 鼠标滚轮", "调整H场景尺寸大小（菜单关闭时）");
            Label(
                _content,
                "菜单打开时，鼠标只操作菜单；F6 / F8 / F9 暂停生效。",
                4,
                _rowY + 10,
                824,
                38,
                16,
                Muted
            );
            _rowY += 48;
        }
        _content.sizeDelta = new Vector2(0, _rowY);
        _scroll.content = _content;
        _scroll.verticalNormalizedPosition = 1f;
        Refresh();
    }

    private void ShortcutRow(string keys, string description)
    {
        Label(_content, keys, 4, _rowY + 4, 220, 27, 17, Foreground, bold: true);
        Label(_content, description, 234, _rowY + 4, 594, 30, 17, Muted);
        _rowY += 34;
    }

    private void ToggleRow(
        string title,
        string description,
        Func<SettingsValues, bool> read,
        Func<SettingsValues, bool, SettingsValues> write
    )
    {
        bool hasDescription = !string.IsNullOrEmpty(description);
        Label(_content, title, 4, _rowY + (hasDescription ? 4 : 18), 688, 26, 20, Foreground);
        if (hasDescription)
            Label(_content, description, 4, _rowY + 33, 688, 29, 16, Muted);
        var button = MakeButton(
            _content,
            "",
            (716, _rowY + 11, 112, 40),
            () => Edit(write(_session.Draft, !read(_session.Draft)))
        );
        _refreshers.Add(() =>
        {
            bool enabled = read(_session.Draft);
            button.Label.text = enabled ? "已开启" : "已关闭";
            button.Background.color = enabled ? Accent : Surface;
        });
        _rowY += hasDescription ? 72 : 64;
    }

    private void FieldRow(
        string title,
        Func<SettingsValues, string> read,
        Func<SettingsValues, string, SettingsValues> write
    )
    {
        Label(_content, title, 4, _rowY + 2, 824, 25, 18, Foreground);
        var field = MakeInput(
            _content,
            value => Edit(write(_session.Draft, value), refreshFields: false)
        );
        Top(field.GetComponent<RectTransform>(), 4, _rowY + 32, 824, 38);
        _refreshers.Add(() => field.SetTextWithoutNotify(read(_session.Draft) ?? ""));
        _rowY += 84;
    }

    private void ScaleRow()
    {
        Label(_content, "H场景尺寸大小", 4, _rowY + 2, 350, 27, 20, Foreground);
        Label(
            _content,
            "立即生效。可输入 10%～1000%，100% 为原始大小。",
            4,
            _rowY + 35,
            460,
            45,
            16,
            Muted
        );
        MakeButton(_content, "−10%", (470, _rowY + 15, 82, 40), () => StepScale(-0.1f));
        var field = MakeInput(
            _content,
            text =>
            {
                float value = float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float percent
                )
                    ? percent / 100f
                    : float.NaN;
                Edit(_session.Draft with { Live2DScale = value }, refreshFields: false);
            }
        );
        Top(field.GetComponent<RectTransform>(), 562, _rowY + 15, 94, 40);
        Label(_content, "%", 659, _rowY + 19, 24, 32, 18, Muted);
        _refreshers.Add(() =>
            field.SetTextWithoutNotify(
                float.IsFinite(_session.Draft.Live2DScale)
                    ? (_session.Draft.Live2DScale * 100).ToString(
                        "0.##",
                        CultureInfo.InvariantCulture
                    )
                    : ""
            )
        );
        MakeButton(_content, "+10%", (691, _rowY + 15, 82, 40), () => StepScale(0.1f));
        var reset = MakeButton(
            _content,
            "重置",
            (781, _rowY + 15, 47, 40),
            () => Edit(_session.Draft with { Live2DScale = 1f })
        );
        reset.Label.fontSize = 16;
        _rowY += 88;
    }

    private void StepScale(float step)
    {
        float current = float.IsFinite(_session.Draft.Live2DScale)
            ? _session.Draft.Live2DScale
            : 1f;
        Edit(
            _session.Draft with
            {
                Live2DScale = Math.Clamp(MathF.Round((current + step) * 100) / 100, 0.1f, 10f),
            }
        );
    }

    private void Edit(SettingsValues values, bool refreshFields = true)
    {
        _session.Draft = values;
        _feedback = null;
        if (refreshFields)
            Refresh();
        else
            RefreshSummary();
    }

    private void Refresh()
    {
        foreach (var refresh in _refreshers)
            refresh();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        _status.text =
            _feedback
            ?? (
                _session.HasChanges
                    ? "有未保存的修改。点击「保存并应用」后才会生效。"
                    : _session.Status
            );
        _status.color =
            (_feedback != null && _feedbackWarning)
            || _session.HasChanges
            || _session.RestartRequired
                ? Warning
                : Muted;
        _closeLabel.text = _session.HasChanges ? "放弃并关闭" : "关闭";
        _save.interactable = _session.HasChanges;
        _reload.interactable = !_session.HasChanges;
    }

    private (Button Button, Text Label, Image Background) MakeButton(
        Transform parent,
        string text,
        (float X, float Y, float Width, float Height) bounds,
        Action click,
        bool permanent = false
    )
    {
        var go = Create(parent, "Button", typeof(Image), typeof(Button));
        Top(go.GetComponent<RectTransform>(), bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var image = go.GetComponent<Image>();
        image.color = Surface;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var label = Label(go.transform, text, 0, 0, 1, 1, 18, Foreground, centered: true);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(4, 2);
        label.rectTransform.offsetMax = new Vector2(-4, -2);
        UnityAction listener = (Action)(() => Run(click));
        button.onClick.AddListener(listener);
        if (permanent)
            _permanentListeners.Add(listener);
        else
            _pageListeners.Add(listener);
        return (button, label, image);
    }

    private InputField MakeInput(Transform parent, Action<string> change)
    {
        var go = Create(parent, "Input", typeof(Image), typeof(InputField));
        var image = go.GetComponent<Image>();
        image.color = Surface;
        var field = go.GetComponent<InputField>();
        field.targetGraphic = image;
        field.navigation = new Navigation { mode = Navigation.Mode.None };
        var text = Label(go.transform, "", 0, 0, 1, 1, 18, Foreground);
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(10, 4);
        text.rectTransform.offsetMax = new Vector2(-10, -4);
        field.textComponent = text;
        field.lineType = InputField.LineType.SingleLine;
        field.characterLimit = 2048;
        UnityAction<string> listener = (Action<string>)(value => Run(() => change(value)));
        field.onValueChanged.AddListener(listener);
        _pageListeners.Add(listener);
        return field;
    }

    private Text Label(
        Transform parent,
        string value,
        float x,
        float y,
        float width,
        float height,
        int size,
        Color color,
        bool bold = false,
        bool centered = false
    )
    {
        var go = Create(parent, "Text", typeof(Text));
        var text = go.GetComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.alignment = centered ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = value;
        Top(text.rectTransform, x, y, width, height);
        return text;
    }

    private void Run(Action action)
    {
        if (_disposed)
            return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error($"Settings menu action failed: {ex}");
            ShowStatus("操作失败，请检查日志；已保存的配置不会自动清空。", warning: true);
        }
    }

    private static GameObject Create(Transform parent, string name, params Type[] extra)
    {
        var types = new Il2CppSystem.Type[extra.Length + 1];
        types[0] = Il2CppType.Of<RectTransform>();
        for (int i = 0; i < extra.Length; i++)
            types[i + 1] = Il2CppType.From(extra[i]);
        var go = new GameObject(name, types) { layer = 5 };
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_root != null)
        {
            _root.SetActive(false);
            UObject.Destroy(_root);
        }
        _permanentListeners.Clear();
        _pageListeners.Clear();
        _refreshers.Clear();
        _fontLease?.Dispose();
    }
}
