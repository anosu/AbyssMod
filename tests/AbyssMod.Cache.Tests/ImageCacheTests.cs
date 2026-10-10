using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AbyssMod;
using AbyssMod.Services;
using Xunit;

namespace CacheTests;

public sealed class ImageCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "AbyssMod-images-" + Guid.NewGuid()
    );

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronizationCommitsOnlyAfterAllImagesPassTheirHashes(
        bool corruptSecondImage
    )
    {
        Directory.CreateDirectory(_root);
        byte[] oldManifest = Encoding.UTF8.GetBytes("old manifest");
        byte[] oldImage = { 1 };
        await File.WriteAllBytesAsync(Path.Combine(_root, "manifest.json"), oldManifest);
        await File.WriteAllBytesAsync(Path.Combine(_root, "a.png"), oldImage);
        byte[] first = { 2, 3 };
        byte[] second = { 4, 5 };
        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(
            new ImageReplacementManifest
            {
                SpriteNames = new() { ["a"] = "a.png", ["b"] = "b.png" },
            }
        );
        byte[] main = JsonSerializer.SerializeToUtf8Bytes(
            new Manifest
            {
                Replacements = new()
                {
                    ["manifest.json"] = Hash(manifest),
                    ["a.png"] = Hash(first),
                    ["b.png"] = Hash(second),
                },
            }
        );
        var files = new Dictionary<string, byte[]>
        {
            ["/manifest/zh_Hans.json"] = main,
            ["/replacements/manifest.json"] = manifest,
            ["/replacements/a.png"] = first,
            ["/replacements/b.png"] = corruptSecondImage ? new byte[] { 99 } : second,
        };
        using var client = new HttpClient(
            new Handler(request => new ByteArrayContent(files[request.RequestUri.AbsolutePath]))
        );
        var cache = new ImageReplacementCache(
            "https://example.test",
            "zh_Hans",
            _root,
            client,
            client
        );
        await cache.SyncAsync();

        Assert.Equal(
            corruptSecondImage ? oldManifest : manifest,
            await File.ReadAllBytesAsync(Path.Combine(_root, "manifest.json"))
        );
        Assert.Equal(
            corruptSecondImage ? oldImage : first,
            await File.ReadAllBytesAsync(Path.Combine(_root, "a.png"))
        );
        Assert.Equal(!corruptSecondImage, File.Exists(Path.Combine(_root, "b.png")));
    }

    [Fact]
    public async Task EncryptedManifestsAreDecodedWhileImageBytesUseThePlainClient()
    {
        byte[] image = Encoding.UTF8.GetBytes("ENC:these-are-raw-image-bytes");
        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(
            new ImageReplacementManifest { SpriteNames = new() { ["a"] = "a.png" } }
        );
        byte[] main = JsonSerializer.SerializeToUtf8Bytes(
            new Manifest
            {
                Replacements = new()
                {
                    ["manifest.json"] = Hash(manifest),
                    ["a.png"] = Hash(image),
                },
            }
        );
        byte[] Encrypt(byte[] value)
        {
            byte[] encoded = (byte[])value.Clone();
            for (int i = 0; i < encoded.Length; i++)
                encoded[i] ^= (byte)'k';
            return Encoding.UTF8.GetBytes("ENC:" + Convert.ToBase64String(encoded));
        }
        var files = new Dictionary<string, byte[]>
        {
            ["/manifest/zh_Hans.json"] = Encrypt(main),
            ["/replacements/manifest.json"] = Encrypt(manifest),
            ["/replacements/a.png"] = image,
        };
        using var plain = new HttpClient(
            new Handler(request => new ByteArrayContent(files[request.RequestUri.AbsolutePath]))
        );
        using var text = new HttpClient(
            new TranslationCryptoHandler(
                new Handler(request => new ByteArrayContent(
                    files[request.RequestUri.AbsolutePath]
                )),
                () => ("ENC:", "k")
            )
        );
        var cache = new ImageReplacementCache(
            "https://example.test",
            "zh_Hans",
            _root,
            plain,
            text
        );
        await cache.SyncAsync();
        Assert.True(File.Exists(Path.Combine(_root, "manifest.json")));
        Assert.Equal(manifest, await File.ReadAllBytesAsync(Path.Combine(_root, "manifest.json")));
        Assert.Equal(image, await File.ReadAllBytesAsync(Path.Combine(_root, "a.png")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedManifestStopsReadingBeforeTheWholeBodyIsBuffered(bool knownLength)
    {
        using var stream = new CountingStream(4 * 1024 * 1024, knownLength);
        using var client = new HttpClient(new Handler(_ => new StreamContent(stream)));
        var cache = new ImageReplacementCache(
            "https://example.test",
            "zh_Hans",
            _root,
            client,
            client
        );
        await cache.SyncAsync();
        if (knownLength)
            Assert.Equal(0, stream.BytesRead);
        else
            Assert.InRange(
                stream.BytesRead,
                ImageReplacementLimits.MaxManifestBytes + 1,
                ImageReplacementLimits.MaxManifestBytes + 81920
            );
        Assert.False(File.Exists(Path.Combine(_root, "manifest.json")));
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpContent> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = content(request) }
            );
    }

    private sealed class CountingStream(long length, bool knownLength) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => knownLength;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = (int)Math.Min(count, length - BytesRead);
            Array.Fill(buffer, (byte)'x', offset, read);
            BytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken token = default
        )
        {
            token.ThrowIfCancellationRequested();
            int read = (int)Math.Min(buffer.Length, length - BytesRead);
            buffer.Span[..read].Fill((byte)'x');
            BytesRead += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
