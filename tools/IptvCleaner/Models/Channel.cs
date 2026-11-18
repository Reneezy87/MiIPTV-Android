using System.Text.RegularExpressions;

namespace IptvCleaner.Models;

/// <summary>
/// Un canal de una lista M3U: la línea #EXTINF, las líneas extra (#EXTVLCOPT, etc.) y la URL del stream.
/// </summary>
public sealed record Channel(string Extinf, IReadOnlyList<string> Extras, string Url)
{
    private string? _name;
    private string[]? _groups;

    /// <summary>Nombre visible del canal (lo que va después de la coma en #EXTINF).</summary>
    public string Name => _name ??= ParseName(Extinf);

    /// <summary>Categorías del canal (group-title separado por ';').</summary>
    public string[] Groups => _groups ??= (GetAttribute(Extinf, "group-title") ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public string PrimaryGroup => Groups.Length > 0 ? Groups[0] : "Undefined";

    public string? TvgId => GetAttribute(Extinf, "tvg-id");

    public string? UserAgent => GetAttribute(Extinf, "http-user-agent") ?? GetVlcOption("http-user-agent");

    public string? Referrer => GetAttribute(Extinf, "http-referrer") ?? GetVlcOption("http-referrer");

    private string? GetVlcOption(string key)
    {
        var prefix = $"#EXTVLCOPT:{key}=";
        var line = Extras.FirstOrDefault(e => e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line?[prefix.Length..].Trim();
    }

    private static string ParseName(string extinf)
    {
        // El nombre va después de la coma que sigue al último atributo entre comillas.
        int lastQuote = extinf.LastIndexOf('"');
        int comma = extinf.IndexOf(',', lastQuote < 0 ? 0 : lastQuote);
        return comma >= 0 ? extinf[(comma + 1)..].Trim() : extinf;
    }

    /// <summary>Lee un atributo key="valor" de una línea M3U.</summary>
    public static string? GetAttribute(string line, string key)
    {
        var match = Regex.Match(line, $@"(?<![\w-]){Regex.Escape(key)}=""([^""]*)""", RegexOptions.IgnoreCase);
        return match.Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : null;
    }
}
