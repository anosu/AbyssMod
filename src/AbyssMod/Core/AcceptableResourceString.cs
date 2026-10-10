#nullable enable
using System;
using BepInEx.Configuration;

namespace AbyssMod;

/// <summary>在 BepInEx 保存和通知变更前规范化资源配置；非法值采用默认值。</summary>
internal sealed class AcceptableResourceString : AcceptableValueBase
{
    private readonly string _defaultValue;
    private readonly Func<string, bool> _isValid;
    private readonly string _description;

    private AcceptableResourceString(
        string defaultValue,
        Func<string, bool> isValid,
        string description
    )
        : base(typeof(string))
    {
        _defaultValue = defaultValue;
        _isValid = isValid;
        _description = description;
    }

    internal static AcceptableResourceString HttpUrl(string defaultValue) =>
        new(
            defaultValue,
            text =>
                Uri.TryCreate(text, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
            "An absolute HTTP or HTTPS URL"
        );

    internal static AcceptableResourceString Path(string defaultValue) =>
        new(defaultValue, text => text.Length > 0, "A non-empty resource path");

    public override object Clamp(object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (_isValid(text))
                return text;
        }
        return _defaultValue;
    }

    public override bool IsValid(object value) => value is string text && _isValid(text.Trim());

    public override string ToDescriptionString() =>
        $"# Acceptable values: {_description}\n"
        + "# Leading/trailing whitespace is trimmed; invalid values use the default.";
}
