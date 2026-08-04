using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MgaAfterDrive.IO;

/// <summary>
/// GitHub Releases から最新バージョンを取得し、現行版と比較する。
/// 自動更新は行わない（通知とリリースページ表示のみ）。
/// </summary>
public static class AppUpdateChecker
{
    public const string GitHubOwner = "mga-ueda";
    public const string GitHubRepo = "MGA-AfterDrive";

    private static readonly Uri LatestReleaseApiUri =
        new($"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest");

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// 最新リリースを問い合わせる。ネットワーク失敗時も例外は投げず結果に収める。
    /// </summary>
    public static async Task<AppUpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);

        try
        {
            using var client = CreateClient();
            using var response = await client
                .GetAsync(LatestReleaseApiUri, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return AppUpdateCheckResult.Failed(
                    currentVersion,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            using var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var root = document.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagElement))
            {
                return AppUpdateCheckResult.Failed(currentVersion, "tag_name がありません。");
            }

            var tagName = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return AppUpdateCheckResult.Failed(currentVersion, "tag_name が空です。");
            }

            var latestVersion = NormalizeVersionLabel(tagName);
            if (!TryParseVersion(latestVersion, out var latest)
                || !TryParseVersion(currentVersion, out var current))
            {
                return AppUpdateCheckResult.Failed(
                    currentVersion,
                    $"バージョンを解釈できません（現行: {currentVersion}, 最新: {latestVersion}）。");
            }

            var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(releaseUrl))
            {
                releaseUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/tag/{tagName.Trim()}";
            }

            var updateAvailable = latest > current;
            return new AppUpdateCheckResult(
                Succeeded: true,
                CurrentVersion: NormalizeVersionLabel(currentVersion),
                LatestVersion: latestVersion,
                ReleaseUrl: releaseUrl,
                UpdateAvailable: updateAvailable,
                ErrorDetail: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException
                or TaskCanceledException
                or OperationCanceledException
                or JsonException
                or IOException)
        {
            return AppUpdateCheckResult.Failed(currentVersion, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public static bool TryOpenUrl(string url, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "URL が空です。";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException
                or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MGA-AfterDrive", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static string NormalizeVersionLabel(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        // 1.0.0-beta+build → 1.0.0（Version.TryParse 用）
        var plus = trimmed.IndexOf('+');
        if (plus >= 0)
        {
            trimmed = trimmed[..plus];
        }

        var dash = trimmed.IndexOf('-');
        if (dash >= 0)
        {
            trimmed = trimmed[..dash];
        }

        return trimmed;
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = NormalizeVersionLabel(value);
        return Version.TryParse(normalized, out version!);
    }
}

/// <summary>
/// <see cref="AppUpdateChecker.CheckAsync"/> の結果。
/// </summary>
public sealed record AppUpdateCheckResult(
    bool Succeeded,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    bool UpdateAvailable,
    string? ErrorDetail)
{
    public static AppUpdateCheckResult Failed(string currentVersion, string errorDetail)
        => new(
            Succeeded: false,
            CurrentVersion: currentVersion,
            LatestVersion: null,
            ReleaseUrl: null,
            UpdateAvailable: false,
            ErrorDetail: errorDetail);
}
