using IptvCleaner.Models;

namespace IptvCleaner.Services;

public sealed record M3uPlaylist(string? Header, IReadOnlyList<Channel> Channels)
{
    /// <summary>URL de la guía EPG declarada en el encabezado, si existe.</summary>
    public string? TvgUrl => Header is null
        ? null
        : Channel.GetAttribute(Header, "x-tvg-url") ?? Channel.GetAttribute(Header, "url-tvg");
}

public static class M3uParser
{
    public static M3uPlaylist Parse(string text)
    {
        var channels = new List<Channel>();
        string? header = null;
        string? extinf = null;
        var extras = new List<string>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                header ??= line;
                continue;
            }

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                extinf = line;
                extras = [];
                continue;
            }

            if (line.StartsWith('#'))
            {
                if (extinf is not null) extras.Add(line);
                continue;
            }

            // Línea de URL
            if (extinf is not null)
            {
                channels.Add(new Channel(extinf, extras, line));
                extinf = null;
                extras = [];
            }
        }

        return new M3uPlaylist(header, channels);
    }
}
