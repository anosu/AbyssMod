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

        if (response.Content != null)
        {
            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            response.Content = new ByteArrayContent(Decrypt(data));
        }

        return response;
    }

    private static byte[] Decrypt(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);

        if (!text.StartsWith(Config.TranslationCryptoTag.Value))
            return data;

        var xor = Convert.FromBase64String(text[Config.TranslationCryptoTag.Value.Length..]);

        return Xor(xor, Encoding.UTF8.GetBytes(Config.TranslationCryptoKey.Value));
    }

    private static byte[] Xor(byte[] data, byte[] key)
    {
        var result = new byte[data.Length];

        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        }

        return result;
    }
}
