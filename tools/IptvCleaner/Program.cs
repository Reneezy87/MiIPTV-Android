using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IptvCleaner;
using IptvCleaner.Models;
using IptvCleaner.Services;

Console.OutputEncoding = Encoding.UTF8;

// ---------- Configuración ----------
Options opt;
try
{
    opt = Options.Load(args);
}
catch (Exception ex) when (ex is ArgumentException or FormatException or FileNotFoundException or JsonException)
{
    Console.Error.WriteLine($"Error: {ex.Message}\n");
    Options.PrintHelp();
    return 1;
}

if (opt.ShowHelp)
{
    Options.PrintHelp();
    return 0;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nCancelando... se guardará lo que se alcanzó a probar.");
};

if (opt.ConfigPath is not null) Console.WriteLine($"Configuración: {opt.ConfigPath}");

using var http = HttpClientFactory.Create(opt.TimeoutSeconds);
var sw = Stopwatch.StartNew();

// ---------- 1. Descargar y parsear ----------
var channels = new List<Channel>();
var seenUrls = new HashSet<string>(StringComparer.Ordinal);
string? tvgUrl = null;

foreach (var src in opt.Sources)
{
    Console.Write($"Leyendo {src} ... ");
    try
    {
        var text = File.Exists(src)
            ? await File.ReadAllTextAsync(src, cts.Token)
            : await http.GetStringAsync(src, cts.Token);

        var playlist = M3uParser.Parse(text);
        tvgUrl ??= playlist.TvgUrl;

        int added = 0;
        foreach (var ch in playlist.Channels)
        {
            if (seenUrls.Add(ch.Url))
            {
                channels.Add(ch);
                added++;
            }
        }
        Console.WriteLine($"{playlist.Channels.Count} canales ({added} nuevos)");
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        return 130;
    }
    catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or UriFormatException or InvalidOperationException)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}

if (channels.Count == 0)
{
    Console.Error.WriteLine("No se obtuvo ningún canal de las listas de origen.");
    return 2;
}

// ---------- 2. Filtrar por categoría ----------
channels = channels.Where(opt.Accepts).ToList();
Console.WriteLine($"\nTotal tras deduplicar y filtrar: {channels.Count} canales");

// ---------- 3. Probar streams ----------
var results = new CheckResult?[channels.Count];

if (opt.SkipCheck)
{
    Array.Fill(results, CheckResult.NotChecked);
}
else
{
    Console.WriteLine($"Probando streams ({opt.Concurrency} a la vez, timeout {opt.TimeoutSeconds}s, {opt.Retries} reintento(s)). Ctrl+C para detener.\n");

    var checker = new StreamChecker(http, TimeSpan.FromSeconds(opt.TimeoutSeconds), opt.UserAgent, opt.Retries);
    var progress = new ProgressPrinter(channels.Count);

    try
    {
        await Parallel.ForEachAsync(
            Enumerable.Range(0, channels.Count),
            new ParallelOptions { MaxDegreeOfParallelism = opt.Concurrency, CancellationToken = cts.Token },
            async (i, ct) =>
            {
                var result = await checker.CheckAsync(channels[i], ct);
                results[i] = result;
                progress.Report(result.Ok);
            });
    }
    catch (OperationCanceledException)
    {
        // Ctrl+C: continuar y guardar lo probado
    }

    progress.Finish();
}

// ---------- 4. Guardar lista y reporte ----------
var working = channels
    .Where((_, i) => results[i]?.Ok == true)
    .OrderBy(ch => ch.PrimaryGroup, StringComparer.OrdinalIgnoreCase)
    .ThenBy(ch => ch.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();

await M3uWriter.WriteAsync(opt.Output, working, tvgUrl);
await ReportWriter.WriteAsync(opt.Report, channels, results);

// ---------- 5. Resumen ----------
int failed = results.Count(r => r is { Ok: false });
int unchecked_ = results.Count(r => r is null);

Console.WriteLine($"\nListo en {sw.Elapsed:mm\\:ss}");
Console.WriteLine($"  Funcionando: {working.Count} de {channels.Count}");
Console.WriteLine($"  Con falla:   {failed}");
if (unchecked_ > 0) Console.WriteLine($"  Sin probar:  {unchecked_} (cancelado)");
Console.WriteLine($"  Lista:       {Path.GetFullPath(opt.Output)}");
Console.WriteLine($"  Reporte:     {Path.GetFullPath(opt.Report)}");

var topFailures = results
    .Where(r => r is { Ok: false })
    .GroupBy(r => r!.Reason)
    .OrderByDescending(g => g.Count())
    .Take(5)
    .ToList();

if (topFailures.Count > 0)
{
    Console.WriteLine("\n  Fallas más comunes:");
    foreach (var g in topFailures)
        Console.WriteLine($"    {g.Count(),5}  {g.Key}");
}

return cts.IsCancellationRequested ? 130 : 0;
