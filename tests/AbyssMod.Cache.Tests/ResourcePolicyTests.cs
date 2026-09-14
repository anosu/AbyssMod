using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AbyssMod.Services;
using Xunit;

namespace CacheTests;

public sealed class ResourcePolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "resource-policy-" + Guid.NewGuid().ToString("N")
    );
    private string ResourcePath => Path.Combine(_root, "novels", "7", "zh_Hans.json");

    // Independent digest for UTF-8 "a\0b\0".
    private const string Digest = "aa3a791e273bce9cf4a2a7caa9028b36";

    [Fact]
    public async Task ListedResourceUsesProtocolHashAndRejectsMismatchingRemoteData()
    {
        using var handler = new Handler(
            "{\"novels\":{\"7\":\"" + Digest.ToUpperInvariant() + "\"}}",
            "{\"a\":\"bad\"}"
        );
        using var client = new HttpClient(handler);
        var cache = new TranslationCache("https://example.test", _root, "zh_Hans", false, client);
        await cache.FetchManifestAsync();
        Assert.Null(await cache.LoadAsync("novels", "7"));
        Assert.False(File.Exists(ResourcePath));
        Assert.Contains("/novels/7/zh_Hans.json", handler.Paths);
        WriteLocal("{\"a\":\"b\"}");
        int before = handler.Paths.Count;
        Assert.Equal("b", (await cache.LoadAsync("novels", "7"))["a"]);
        Assert.Equal(before, handler.Paths.Count);
    }

    [Fact]
    public async Task UnlistedResourceReadsLocalWithoutDownloading()
    {
        using var handler = new Handler("{}", "{\"a\":\"remote\"}");
        using var client = new HttpClient(handler);
        var cache = new TranslationCache("https://example.test", _root, "zh_Hans", false, client);
        await cache.FetchManifestAsync();
        Assert.Null(await cache.LoadAsync("novels", "7"));
        WriteLocal("{\"a\":\"local\"}");
        Assert.Equal("local", (await cache.LoadAsync("novels", "7"))["a"]);
        Assert.DoesNotContain("/novels/7/zh_Hans.json", handler.Paths);
    }

    [Fact]
    public async Task PreferredLocalFilesCanBeEditedBetweenLoads()
    {
        using var handler = new Handler("{\"novels\":{\"7\":\"expected\"}}", "{}");
        using var client = new HttpClient(handler);
        var cache = new TranslationCache("https://example.test", _root, "zh_Hans", true, client);
        await cache.FetchManifestAsync();
        WriteLocal("{\"a\":\"one\"}");
        Assert.Equal("one", (await cache.LoadAsync("novels", "7"))["a"]);
        WriteLocal("{\"a\":\"two\"}");
        Assert.Equal("two", (await cache.LoadAsync("novels", "7"))["a"]);
        Assert.DoesNotContain("/novels/7/zh_Hans.json", handler.Paths);
    }

    private void WriteLocal(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResourcePath));
        File.WriteAllText(ResourcePath, json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private sealed class Handler(string manifest, string resource) : HttpMessageHandler
    {
        public readonly List<string> Paths = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        )
        {
            Paths.Add(request.RequestUri.AbsolutePath);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        request.RequestUri.AbsolutePath.Contains("/manifest/") ? manifest : resource
                    ),
                }
            );
        }
    }
}
