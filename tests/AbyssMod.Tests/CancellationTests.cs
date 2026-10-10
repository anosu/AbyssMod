using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AbyssMod.Services;
using Xunit;

namespace AbyssMod.Tests;

public sealed class CancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownCancelsRequestsWithoutPublishing(bool novel)
    {
        string root = Path.Combine(Path.GetTempPath(), "abyss-cancel-" + Guid.NewGuid());
        try
        {
            using var handler = new PendingHandler();
            using var client = new HttpClient(handler);
            var manager = new TranslationManager(
                new TranslationCache("https://example.test", root, "zh_Hans", false, client),
                false,
                false
            );
            Task loading = novel
                ? manager.GetNovelTranslationAsync("7")
                : manager.EnsureStaticTranslationsLoadedAsync();
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            manager.Shutdown();
            manager.Shutdown();
            await loading.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(handler.Cancelled);
            Assert.False(manager.HasUiTranslations);
            Assert.False(manager.MasterDataTranslationReady);
            Assert.False(manager.HasNovel("7"));
            await manager.EnsureStaticTranslationsLoadedAsync();
            await manager.GetNovelTranslationAsync("8");
            Assert.Equal(1, handler.Requests);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private sealed class PendingHandler : HttpMessageHandler
    {
        internal readonly TaskCompletionSource Started = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        internal bool Cancelled;
        internal int Requests;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        )
        {
            Requests++;
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                Cancelled = true;
                throw;
            }
            throw new InvalidOperationException("Request must be cancelled");
        }
    }
}
