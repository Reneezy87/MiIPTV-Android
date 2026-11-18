namespace IptvCleaner.Models;

/// <summary>Resultado de probar un stream.</summary>
public sealed record CheckResult(bool Ok, string Reason)
{
    public static readonly CheckResult Success = new(true, "OK");
    public static readonly CheckResult NotChecked = new(true, "sin probar");

    public static CheckResult Fail(string reason) => new(false, reason);
}
