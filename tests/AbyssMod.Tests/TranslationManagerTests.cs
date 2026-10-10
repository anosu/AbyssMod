using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AbyssMod.Services;
using Xunit;

namespace AbyssMod.Tests;

public sealed class TranslationManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "AbyssMod-translation-" + Guid.NewGuid()
    );

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StaticLoadingUsesConstructorOptionsAndMergesConcurrentRequests(
        bool masterData,
        bool ui
    )
    {
        using var handler = new Handler();
        using var client = new HttpClient(handler);
        var cache = new TranslationCache("https://example.test", _root, "zh_Hans", false, client);
        var manager = new TranslationManager(cache, masterData, ui);
        await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => manager.EnsureStaticTranslationsLoadedAsync())
        );
        Assert.Equal(1, handler.Paths.Count(path => path.Contains("/manifest/")));
        Assert.Equal(1, handler.Paths.Count(path => path.Contains("/names/")));
        Assert.Equal(masterData ? 1 : 0, handler.Paths.Count(path => path.Contains("/static/")));
        Assert.Equal(ui ? 1 : 0, handler.Paths.Count(path => path.Contains("/ui_texts/")));
        Assert.Equal(masterData, manager.MasterDataTranslationReady);
        Assert.Equal(ui, manager.HasUiTranslations);
    }

    [Fact]
    public async Task ConcurrentNovelLoadsPublishOneSharedTranslation()
    {
        using var handler = new Handler();
        using var client = new HttpClient(handler);
        var cache = new TranslationCache("https://example.test", _root, "zh_Hans", false, client);
        var manager = new TranslationManager(cache, false, false);
        await manager.EnsureStaticTranslationsLoadedAsync();
        await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => manager.GetNovelTranslationAsync("7"))
        );
        Assert.Equal(1, handler.Paths.Count(path => path.Contains("/novels/")));
        Assert.True(manager.TryGetNovel("7", out var translations));
        Assert.Equal("translated", translations["Original"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public readonly ConcurrentBag<string> Paths = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        )
        {
            string path = request.RequestUri.AbsolutePath;
            Paths.Add(path);
            string novelHash = Convert
                .ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("Original\0translated\0")))
                .ToLowerInvariant();
            string json =
                path.Contains("/manifest/")
                    ? "{\"names\":\"d41d8cd98f00b204e9800998ecf8427e\",\"static\":\"d41d8cd98f00b204e9800998ecf8427e\",\"ui_texts\":\"d41d8cd98f00b204e9800998ecf8427e\",\"novels\":{\"7\":\""
                        + novelHash
                        + "\"}}"
                : path.Contains("/novels/") ? "{\"Original\":\"translated\"}"
                : "{}";
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
            );
        }
    }
}
