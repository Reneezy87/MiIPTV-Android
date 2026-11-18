using System.Text.Json;
using System.Text.Json.Serialization;
using IptvCleaner.Models;

namespace IptvCleaner;

/// <summary>
/// Configuración de la ejecución. Se carga de config.json y la línea de comandos la sobreescribe.
/// </summary>
public sealed class Options
{
    public static readonly string[] DefaultSources =
    [
        "https://iptv-org.github.io/iptv/countries/mx.m3u",
        "https://iptv-org.github.io/iptv/languages/spa.m3u",
    ];

    public List<string> Sources { get; set; } = [];
    public string Output { get; set; } = "mi-lista.m3u";
    public string Report { get; set; } = "reporte.csv";
    public List<string> Include { get; set; } = [];
    public List<string> Exclude { get; set; } = [];
    public int Concurrency { get; set; } = 30;
    public int TimeoutSeconds { get; set; } = 10;
    public int Retries { get; set; } = 1;
    public bool SkipCheck { get; set; }
    public string UserAgent { get; set; } = "VLC/3.0.20 LibVLC/3.0.20";

    [JsonIgnore] public bool ShowHelp { get; private set; }
    [JsonIgnore] public string? ConfigPath { get; private set; }

    private HashSet<string>? _include;
    private HashSet<string>? _exclude;

    /// <summary>¿El canal pasa los filtros de categoría?</summary>
    public bool Accepts(Channel channel)
    {
        _include ??= Include.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _exclude ??= Exclude.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var groups = channel.Groups;
        if (_include.Count > 0 && !groups.Any(_include.Contains)) return false;
        if (_exclude.Count > 0 && groups.Any(_exclude.Contains)) return false;
        return true;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Options Load(string[] args)
    {
        // 1) Ubicar config.json (--config explícito, directorio actual o junto al ejecutable)
        string? explicitConfig = null;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--config") explicitConfig = args[i + 1];

        string? configPath = explicitConfig;
        if (configPath is not null && !File.Exists(configPath))
            throw new FileNotFoundException($"No existe el archivo de configuración: {configPath}");

        configPath ??= new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
                Path.Combine(AppContext.BaseDirectory, "config.json"),
            }
            .FirstOrDefault(File.Exists);

        var opt = configPath is null
            ? new Options()
            : JsonSerializer.Deserialize<Options>(File.ReadAllText(configPath), JsonOptions) ?? new Options();
        opt.ConfigPath = configPath;

        // 2) Línea de comandos
        bool sourcesFromCli = false;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string Next() => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"Falta el valor para {arg}");

            switch (arg)
            {
                case "--config": Next(); break; // ya procesado
                case "--src":
                    if (!sourcesFromCli) { opt.Sources.Clear(); sourcesFromCli = true; }
                    opt.Sources.Add(Next());
                    break;
                case "--out": opt.Output = Next(); break;
                case "--report": opt.Report = Next(); break;
                case "--groups": opt.Include = SplitList(Next()); break;
                case "--exclude": opt.Exclude = SplitList(Next()); break;
                case "--concurrency": opt.Concurrency = int.Parse(Next()); break;
                case "--timeout": opt.TimeoutSeconds = int.Parse(Next()); break;
                case "--retries": opt.Retries = int.Parse(Next()); break;
                case "--user-agent": opt.UserAgent = Next(); break;
                case "--no-check": opt.SkipCheck = true; break;
                case "-h":
                case "--help": opt.ShowHelp = true; break;
                default: throw new ArgumentException($"Opción desconocida: {arg}");
            }
        }

        if (opt.Sources.Count == 0) opt.Sources.AddRange(DefaultSources);
        if (opt.Concurrency < 1) throw new ArgumentException("--concurrency debe ser mayor a 0");
        if (opt.TimeoutSeconds < 1) throw new ArgumentException("--timeout debe ser mayor a 0");
        if (opt.Retries < 0) throw new ArgumentException("--retries no puede ser negativo");

        return opt;
    }

    private static List<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static void PrintHelp()
    {
        Console.WriteLine("""
            IptvCleaner - genera una lista M3U limpia con solo los canales que funcionan

            Uso:
              IptvCleaner [opciones]

            Opciones:
              --config <archivo>    Archivo de configuración (default: config.json)
              --src <url|archivo>   Lista de origen; se puede repetir (reemplaza las de config.json)
              --out <archivo>       Lista de salida (default: mi-lista.m3u)
              --report <archivo>    Reporte CSV (default: reporte.csv)
              --groups <a,b,c>      Solo estas categorías, ej. News,Music,Kids
              --exclude <a,b,c>     Excluir categorías, ej. Religious,Shop
              --concurrency <n>     Pruebas simultáneas (default: 30)
              --timeout <seg>       Timeout por stream (default: 10)
              --retries <n>         Reintentos si falla por timeout o conexión (default: 1)
              --user-agent <ua>     User-Agent por defecto
              --no-check            No probar streams
              -h, --help            Mostrar esta ayuda

            Ejemplos:
              IptvCleaner --groups News,Music --concurrency 50
              IptvCleaner --src https://iptv-org.github.io/iptv/categories/news.m3u --out noticias.m3u
            """);
    }
}
