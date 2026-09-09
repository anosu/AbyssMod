using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AbyssMod.Services;

public sealed class CryptoHandler : DelegatingHandler
{
    public CryptoHandler(HttpMessageHandler innerHandler)
        : base(innerHandler) { }

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
                var data = await originalContent.ReadAsByteArrayAsync(cancellationToken);
                var decrypted = Decrypt(data);
                if (!ReferenceEquals(data, decrypted))
                {
                    var replacement = new ByteArrayContent(decrypted);
                    replacement.Headers.ContentType = originalContent.Headers.ContentType;
                    response.Content = replacement;
                    originalContent.Dispose();
                }
            }

            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static byte[] Decrypt(byte[] data)
    {
        string tag = Config.TranslationCryptoTag.Value;
        if (string.IsNullOrEmpty(tag))
            return data;

        var tagBytes = Encoding.UTF8.GetBytes(tag);
        if (
            data.Length < tagBytes.Length
            || !data.AsSpan(0, tagBytes.Length).SequenceEqual(tagBytes)
        )
            return data;

        string key = Config.TranslationCryptoKey.Value;
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
