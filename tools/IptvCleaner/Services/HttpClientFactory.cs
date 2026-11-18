using System.Net;

namespace IptvCleaner.Services;

public static class HttpClientFactory
{
    public static HttpClient Create(int timeoutSeconds)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            ConnectTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 8,
        };

        return new HttpClient(handler)
        {
            // El timeout se controla por petición en StreamChecker; aquí solo un límite general para descargas de listas.
            Timeout = TimeSpan.FromMinutes(2),
        };
    }
}
