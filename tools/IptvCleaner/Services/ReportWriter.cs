using System.Text;
using IptvCleaner.Models;

namespace IptvCleaner.Services;

public static class ReportWriter
{
    public static async Task WriteAsync(string path, IReadOnlyList<Channel> channels, IReadOnlyList<CheckResult?> results)
    {
        M3uWriter.EnsureDirectory(path);

        var sb = new StringBuilder("nombre,tvg_id,categoria,estado,detalle,url\n");
        for (int i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            var r = results[i];
            string estado = r is null ? "SIN PROBAR" : r.Ok ? "OK" : "FALLA";

            sb.AppendLine(string.Join(",",
                Csv(ch.Name),
                Csv(ch.TvgId ?? ""),
                Csv(string.Join(";", ch.Groups)),
                estado,
                Csv(r?.Reason ?? "cancelado"),
                Csv(ch.Url)));
        }

        // Con BOM para que Excel muestre bien los acentos
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
