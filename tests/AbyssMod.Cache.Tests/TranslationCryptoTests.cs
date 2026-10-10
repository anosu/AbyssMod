using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AbyssMod.Services;
using Xunit;

namespace CacheTests;

public sealed class TranslationCryptoTests
{
    [Fact]
    public async Task OnlyTheTranslationPipelineDecodesAndItReadsCurrentSettings()
    {
        (string Tag, string Key) settings = ("ENC:", "first");
        byte[] content = Encode("translated", settings.Tag, settings.Key);
        using var transport = new Handler(() => content);
        using var raw = new HttpClient(transport, disposeHandler: false);
        using var translated = new HttpClient(
            new TranslationCryptoHandler(transport, () => settings)
        );
        Assert.Equal(content, await raw.GetByteArrayAsync("https://example.test/image.png"));
        Assert.Equal(
            "translated",
            await translated.GetStringAsync("https://example.test/text.json")
        );

        settings = ("NEW:", "second");
        content = Encode("updated", settings.Tag, settings.Key);
        Assert.Equal("updated", await translated.GetStringAsync("https://example.test/text.json"));
        content = Encoding.UTF8.GetBytes("plain");
        Assert.Equal("plain", await translated.GetStringAsync("https://example.test/text.json"));
    }

    private static byte[] Encode(string text, string tag, string key)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] ^= keyBytes[i % keyBytes.Length];
        return Encoding.UTF8.GetBytes(tag + Convert.ToBase64String(bytes));
    }

    private sealed class Handler(Func<byte[]> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new MemoryStream(content())),
                }
            );
    }
}
