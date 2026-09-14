using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AbyssMod.Services;

internal sealed class UiTextIndex
{
    private static readonly Regex PlaceholderRegex = new(@"\{(\d+)\}", RegexOptions.Compiled);
    private readonly Dictionary<string, UiTextPathRules> _exactPaths = new(StringComparer.Ordinal);
    private readonly List<UiTextPathRules> _wildcardPaths = new();

    public UiTextIndex(Dictionary<string, Dictionary<string, string>> table)
    {
        foreach (var (path, translations) in table)
        {
            if (translations == null)
                continue;

            var rules = new UiTextPathRules(path, translations);
            if (rules.IsWildcard)
                _wildcardPaths.Add(rules);
            else
                _exactPaths[path] = rules;
        }

        _wildcardPaths.Sort(static (left, right) => right.Specificity.CompareTo(left.Specificity));
    }

    public bool TryTranslate(string transformPath, string sourceText, out string translatedText)
    {
        translatedText = null;
        if (string.IsNullOrEmpty(transformPath) || string.IsNullOrEmpty(sourceText))
            return false;

        if (
            _exactPaths.TryGetValue(transformPath, out var exactRules)
            && exactRules.TryTranslate(sourceText, out translatedText)
        )
            return true;

        foreach (var rules in _wildcardPaths)
        {
            if (
                rules.MatchesPath(transformPath)
                && rules.TryTranslate(sourceText, out translatedText)
            )
                return true;
        }

        return false;
    }

    private sealed class UiTextPathRules
    {
        private readonly Regex _pathRegex;
        private readonly Dictionary<string, string> _exactTexts = new(StringComparer.Ordinal);
        private readonly List<UiTextPattern> _patterns = new();

        public UiTextPathRules(string path, Dictionary<string, string> translations)
        {
            Path = path;
            IsWildcard = path.Contains('*', StringComparison.Ordinal);
            Specificity = path.Length - path.Count(character => character == '*');
            if (IsWildcard)
                _pathRegex = new Regex(
                    BuildPathPattern(path),
                    RegexOptions.Compiled | RegexOptions.CultureInvariant
                );

            foreach (var (sourceText, translatedText) in translations)
            {
                if (string.IsNullOrEmpty(translatedText))
                    continue;

                if (PlaceholderRegex.IsMatch(sourceText))
                    _patterns.Add(new UiTextPattern(sourceText, translatedText));
                else
                    _exactTexts[sourceText] = translatedText;
            }
        }

        public string Path { get; }
        public bool IsWildcard { get; }
        public int Specificity { get; }

        public bool MatchesPath(string transformPath) =>
            !IsWildcard || _pathRegex.IsMatch(transformPath);

        public bool TryTranslate(string sourceText, out string translatedText)
        {
            if (_exactTexts.TryGetValue(sourceText, out translatedText))
                return true;

            foreach (var pattern in _patterns)
                if (pattern.TryTranslate(sourceText, out translatedText))
                    return true;

            translatedText = null;
            return false;
        }

        private static string BuildPathPattern(string path)
        {
            var pattern = new StringBuilder("^");
            foreach (char character in path)
                pattern.Append(character == '*' ? "[^/]*" : Regex.Escape(character.ToString()));
            pattern.Append('$');
            return pattern.ToString();
        }
    }

    private sealed class UiTextPattern
    {
        private readonly Regex _sourceRegex;
        private readonly string _translatedTemplate;

        public UiTextPattern(string sourceTemplate, string translatedTemplate)
        {
            _sourceRegex = new Regex(
                BuildSourcePattern(sourceTemplate),
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline
            );
            _translatedTemplate = translatedTemplate;
        }

        public bool TryTranslate(string sourceText, out string translatedText)
        {
            var match = _sourceRegex.Match(sourceText);
            if (!match.Success)
            {
                translatedText = null;
                return false;
            }

            translatedText = PlaceholderRegex.Replace(
                _translatedTemplate,
                matchResult =>
                {
                    string groupName = $"p{matchResult.Groups[1].Value}";
                    return match.Groups[groupName].Success
                        ? match.Groups[groupName].Value
                        : matchResult.Value;
                }
            );
            return !string.IsNullOrEmpty(translatedText);
        }

        private static string BuildSourcePattern(string sourceTemplate)
        {
            var pattern = new StringBuilder("^");
            var seenPlaceholders = new HashSet<string>(StringComparer.Ordinal);
            int lastIndex = 0;
            foreach (Match match in PlaceholderRegex.Matches(sourceTemplate))
            {
                pattern.Append(
                    Regex.Escape(sourceTemplate.Substring(lastIndex, match.Index - lastIndex))
                );

                string groupName = $"p{match.Groups[1].Value}";
                pattern.Append(
                    seenPlaceholders.Add(groupName) ? $"(?<{groupName}>.+?)" : $"\\k<{groupName}>"
                );
                lastIndex = match.Index + match.Length;
            }

            pattern.Append(Regex.Escape(sourceTemplate.Substring(lastIndex)));
            pattern.Append('$');
            return pattern.ToString();
        }
    }
}
