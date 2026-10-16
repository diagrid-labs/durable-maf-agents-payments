namespace InvoicePayments.Tests;

internal static class TestPaths
{
    // Walk up from bin/Debug/net10.0 to the repository root and use its data folder.
    public static string DataDir { get; } = FindDataDir();

    private static string FindDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "invoices")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }
}
