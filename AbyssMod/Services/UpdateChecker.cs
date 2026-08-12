using System;
using System.Net.Http;
using System.Threading.Tasks;
using Utility.Toast;

namespace AbyssMod.Services;

/// <summary>通过 GitHub 最新 Release 的重定向检查插件更新。</summary>
internal static class UpdateChecker
{
    private const string LatestReleaseUrl =
        "https://github.com/anosu/AbyssMod/releases/latest";
    private const string ReleaseTagPath = "/releases/tag/";

    public static async Task CheckAsync(HttpClient httpClient, string currentVersion)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, LatestReleaseUrl);
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn(
                    $"Update check failed: {(int)response.StatusCode} {response.StatusCode}"
                );
                return;
            }

            Uri releaseUri = response.RequestMessage?.RequestUri;
            if (!TryGetVersion(releaseUri, out string latestVersion))
            {
                Logger.Warn($"Update check returned an unexpected URL: {releaseUri}");
                return;
            }

            if (!IsNewerVersion(currentVersion, latestVersion))
                return;

            Logger.Info(
                $"New version available: {currentVersion} -> {latestVersion}. "
                    + $"Release: {releaseUri}"
            );
            Toast.Info(
                "发现 AbyssMod 新版本",
                $"当前版本：{NormalizeVersion(currentVersion)}\n最新版本：{latestVersion}"
            );
        }
        catch (TaskCanceledException)
        {
            Logger.Warn("Update check timed out");
        }
        catch (Exception e)
        {
            Logger.Warn($"Update check failed: {e.Message}");
        }
    }

    internal static bool TryGetVersion(Uri releaseUri, out string version)
    {
        version = null;
        if (releaseUri == null)
            return false;

        string path = releaseUri.AbsolutePath.TrimEnd('/');
        int tagIndex = path.IndexOf(ReleaseTagPath, StringComparison.OrdinalIgnoreCase);
        if (tagIndex < 0)
            return false;

        string tag = Uri.UnescapeDataString(path[(tagIndex + ReleaseTagPath.Length)..]);
        if (string.IsNullOrWhiteSpace(tag) || tag.Contains('/'))
            return false;

        version = NormalizeVersion(tag);
        return !string.IsNullOrEmpty(version);
    }

    internal static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        string current = NormalizeVersion(currentVersion);
        string latest = NormalizeVersion(latestVersion);
        if (
            string.IsNullOrEmpty(current)
            || string.IsNullOrEmpty(latest)
            || string.Equals(current, latest, StringComparison.OrdinalIgnoreCase)
        )
            return false;

        if (!Version.TryParse(GetNumericVersion(current), out Version parsedCurrent))
        {
            Logger.Warn($"Unable to parse current plugin version: {currentVersion}");
            return false;
        }
        if (!Version.TryParse(GetNumericVersion(latest), out Version parsedLatest))
        {
            Logger.Warn($"Unable to parse latest plugin version: {latestVersion}");
            return false;
        }

        return parsedLatest > parsedCurrent;
    }

    private static string NormalizeVersion(string version)
    {
        version = version?.Trim();
        return !string.IsNullOrEmpty(version) && (version[0] == 'v' || version[0] == 'V')
            ? version[1..]
            : version;
    }

    private static string GetNumericVersion(string version)
    {
        int suffixIndex = version.IndexOfAny(new[] { '-', '+' });
        return suffixIndex >= 0 ? version[..suffixIndex] : version;
    }
}
