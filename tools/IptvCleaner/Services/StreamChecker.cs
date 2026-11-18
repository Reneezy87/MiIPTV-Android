using System.Text;
using IptvCleaner.Models;

namespace IptvCleaner.Services;

/// <summary>
/// Prueba si un stream responde: descarga solo los primeros KB y valida que sea video o un HLS válido.
/// Para listas maestras HLS también prueba la primera variante.
/// </summary>
public sealed class StreamChecker(HttpClient http, TimeSpan timeout, string defaultUserAgent)
{
    private const int MaxBytesToRead = 16 * 1024;

    public async Task<CheckResult> CheckAsync(Channel channel, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await CheckUrlAsync(channel.Url, channel, depth: 0, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CheckResult.Fail("timeout");
        }
        catch (HttpRequestException ex)
        {
            return CheckResult.Fail(ex.HttpRequestError.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CheckResult.Fail(ex.GetType().Name);
        }
    }

    private async Task<CheckResult> CheckUrlAsync(string url, Channel channel, int depth, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return CheckResult.Fail("URL no soportada");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", channel.UserAgent ?? defaultUserAgent);
        if (channel.Referrer is { } referrer)
            request.Headers.TryAddWithoutValidation("Referer", referrer);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            return CheckResult.Fail($"HTTP {(int)response.StatusCode}");

        var buffer = new byte[MaxBytesToRead];
        int read = 0;
        await using (var stream = await response.Content.ReadAsStreamAsync(ct))
        {
            int n;
            while (read < buffer.Length && (n = await stream.ReadAsync(buffer.AsMemory(read), ct)) > 0)
                read += n;
        }

        if (read == 0) return CheckResult.Fail("respuesta vacía");

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var finalUri = response.RequestMessage?.RequestUri ?? uri;
        bool isHls = finalUri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                     || contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase);

        if (!isHls)
        {
            return contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                ? CheckResult.Fail("devuelve HTML")
                : CheckResult.Success; // TS/MP4/etc. que ya está enviando datos
        }

        var body = Encoding.UTF8.GetString(buffer, 0, read).TrimStart('﻿', ' ', '\r', '\n', '\t');
        if (!body.StartsWith("#EXTM3U", StringComparison.Ordinal))
            return CheckResult.Fail("m3u8 inválido");

        // Lista maestra: probar la primera variante (muchos canales fallan ahí por geobloqueo)
        if (body.Contains("#EXT-X-STREAM-INF", StringComparison.Ordinal))
        {
            if (depth > 0) return CheckResult.Fail("lista maestra anidada");

            var variant = body.Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));
            if (variant is null) return CheckResult.Fail("lista maestra sin variantes");

            var variantUrl = new Uri(finalUri, variant).ToString();
            var result = await CheckUrlAsync(variantUrl, channel, depth + 1, ct);
            return result.Ok ? result : result with { Reason = "variante: " + result.Reason };
        }

        if (!body.Contains("#EXTINF", StringComparison.Ordinal) &&
            !body.Contains("#EXT-X-TARGETDURATION", StringComparison.Ordinal))
            return CheckResult.Fail("m3u8 sin segmentos");

        return CheckResult.Success;
    }
}
