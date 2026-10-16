using Dapr.Workflow;
using InvoicePayments.ApiService.Demo;
using InvoicePayments.ApiService.Models;
using InvoicePayments.ApiService.Report;

namespace InvoicePayments.ApiService.Activities;

internal sealed partial class WritePaymentReportActivity(ILogger<WritePaymentReportActivity> logger)
    : WorkflowActivity<WriteReportInput, WriteReportOutput>
{
    public override async Task<WriteReportOutput> RunAsync(WorkflowActivityContext context, WriteReportInput input)
    {
        var dir = OutputPaths.OutputDirectory();
        Directory.CreateDirectory(dir);
        // A fixed path per run: writing the same report twice leaves the same file, so this
        // activity is idempotent without any extra bookkeeping.
        var path = Path.Combine(dir, $"payment-run-{input.RunId}.md");

        await File.WriteAllTextAsync(path, PaymentReportWriter.Render(input.RunId, input.Summary, input.Outcomes));

        LogWrote(logger, path, input.Outcomes.Count);
        return new WriteReportOutput(path);
    }

    [LoggerMessage(LogLevel.Information, "Wrote payment run report to {Path} with {Count} invoices")]
    static partial void LogWrote(ILogger logger, string path, int count);
}
