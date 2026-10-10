#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AbyssMod.Services;

/// <summary>在分配完整响应体前执行大小限制，也覆盖未提供 Content-Length 的响应。</summary>
internal static class HttpContentReader
{
    internal static async Task<byte[]> ReadAsync(
        HttpContent content,
        long maxBytes,
        CancellationToken token
    )
    {
        if (content.Headers.ContentLength > maxBytes)
            throw new InvalidDataException($"download exceeds {maxBytes} bytes");
        using var stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var body = new MemoryStream();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            if (body.Length + read > maxBytes)
                throw new InvalidDataException($"download exceeds {maxBytes} bytes");
            body.Write(buffer, 0, read);
        }
        return body.ToArray();
    }
}
