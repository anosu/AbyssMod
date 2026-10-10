using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AbyssMod.Services;

internal sealed class TranslationCryptoHandler : DelegatingHandler
{
    private const long MaxResponseBytes = 64L * 1024 * 1024;
    private readonly Func<(string Tag, string Key)> _getSettings;

    public TranslationCryptoHandler(
        HttpMessageHandler innerHandler,
        Func<(string Tag, string Key)> getSettings
    )
        : base(innerHandler) => _getSettings = getSettings;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            if (response.Content != null)
            {
                var originalContent = response.Content;
                var data = await HttpContentReader.ReadAsync(
                    originalContent,
                    MaxResponseBytes,
                    cancellationToken
                );
                var decrypted = Decrypt(data);
                // 限量读取消耗了原始网络流；未加密的响应也必须替换为可再次读取的内容。
                var replacement = new ByteArrayContent(decrypted);
                if (ReferenceEquals(data, decrypted))
                {
                    foreach (var header in originalContent.Headers)
                        replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                else
                    replacement.Headers.ContentType = originalContent.Headers.ContentType;
                response.Content = replacement;
                originalContent.Dispose();
            }

            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private byte[] Decrypt(byte[] data)
    {
        var (tag, key) = _getSettings();
        if (string.IsNullOrEmpty(tag))
            return data;

        var tagBytes = Encoding.UTF8.GetBytes(tag);
        if (
            data.Length < tagBytes.Length
            || !data.AsSpan(0, tagBytes.Length).SequenceEqual(tagBytes)
        )
            return data;

        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("Translation crypto key cannot be empty");

        var decrypted = Convert.FromBase64String(
            Encoding.UTF8.GetString(data, tagBytes.Length, data.Length - tagBytes.Length)
        );
        var keyBytes = Encoding.UTF8.GetBytes(key);

        for (int i = 0; i < decrypted.Length; i++)
            decrypted[i] ^= keyBytes[i % keyBytes.Length];

        return decrypted;
    }
}
