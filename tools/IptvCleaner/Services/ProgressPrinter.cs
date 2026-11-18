namespace IptvCleaner.Services;

/// <summary>Muestra el avance en una sola línea de consola. Seguro para uso concurrente.</summary>
public sealed class ProgressPrinter(int total)
{
    private int _done;
    private int _ok;
    private readonly object _lock = new();

    public void Report(bool ok)
    {
        if (ok) Interlocked.Increment(ref _ok);
        int done = Interlocked.Increment(ref _done);
        if (done % 10 == 0 || done == total) Print(done);
    }

    public void Finish()
    {
        Print(Volatile.Read(ref _done));
        Console.WriteLine();
    }

    private void Print(int done)
    {
        lock (_lock)
        {
            int pct = total == 0 ? 100 : done * 100 / total;
            Console.Write($"\r  [{done}/{total}] {pct,3}%  funcionando: {Volatile.Read(ref _ok)}   ");
        }
    }
}
