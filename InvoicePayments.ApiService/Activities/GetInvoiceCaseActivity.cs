using Dapr.Workflow;
using InvoicePayments.ApiService.Checks;
using InvoicePayments.ApiService.Data;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Activities;

// Loads the invoice and its purchase order and runs the deterministic checks. I/O belongs in
// an activity, never in the workflow body, so its result is checkpointed and replayed.
internal sealed partial class GetInvoiceCaseActivity(
    InvoiceDataReader reader,
    ILogger<GetInvoiceCaseActivity> logger)
    : WorkflowActivity<GetInvoiceCaseInput, InvoiceCase>
{
    public override Task<InvoiceCase> RunAsync(WorkflowActivityContext context, GetInvoiceCaseInput input)
    {
        var invoiceId = input.InvoiceId;
        var invoice = reader.GetInvoice(invoiceId);
        var po = reader.FindPurchaseOrder(invoice.PoNumber);
        var checks = InvoiceChecks.Run(invoice, po);

        LogChecked(logger, invoiceId, checks.Passed ? "none" : string.Join(", ", checks.Flags));
        return Task.FromResult(new InvoiceCase(invoice, po, checks));
    }

    [LoggerMessage(LogLevel.Information, "🔎 Checked {InvoiceId}, flags: {Flags}")]
    static partial void LogChecked(ILogger logger, string invoiceId, string flags);
}
