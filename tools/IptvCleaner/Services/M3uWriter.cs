using System.Text;
using IptvCleaner.Models;

namespace IptvCleaner.Services;

public static class M3uWriter
{
    public static async Task WriteAsync(string path, IEnumerable<Channel> channels, string? tvgUrl)
    {
        EnsureDirectory(path);

        var sb = new StringBuilder();
        sb.AppendLine(tvgUrl is null ? "#EXTM3U" : $"#EXTM3U x-tvg-url=\"{tvgUrl}\"");

        foreach (var ch in channels)
        {
            sb.AppendLine(ch.Extinf);
            foreach (var extra in ch.Extras) sb.AppendLine(extra);
            sb.AppendLine(ch.Url);
        }

        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    internal static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }
}
