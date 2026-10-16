namespace InvoicePayments.ApiService.Demo;

// Single source of truth for where demo artifacts land, so the payment-run report and the
// agent-call ledger always co-locate. The AppHost sets OUTPUT_DIR to the repo's out folder.
public static class OutputPaths
{
    public static string OutputDirectory() =>
        Environment.GetEnvironmentVariable("OUTPUT_DIR")
        ?? Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName, "out");
}
