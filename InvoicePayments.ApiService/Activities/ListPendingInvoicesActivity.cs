using Dapr.Workflow;
using InvoicePayments.ApiService.Data;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Activities;

internal sealed partial class ListPendingInvoicesActivity(
    InvoiceDataReader reader,
    ILogger<ListPendingInvoicesActivity> logger)
    : WorkflowActivity<ListPendingInvoicesInput, IReadOnlyList<InvoiceSummary>>
{
    public override Task<IReadOnlyList<InvoiceSummary>> RunAsync(WorkflowActivityContext context, ListPendingInvoicesInput input)
    {
        IReadOnlyList<InvoiceSummary> items = reader.ListInvoiceIds(input.MaxInvoices)
            .Select(reader.GetInvoice)
            .Select(i => new InvoiceSummary(i.InvoiceId, i.SupplierName, i.Amount, i.Currency))
            .ToList();

        LogListing(logger, items.Count);
        return Task.FromResult(items);
    }

    [LoggerMessage(LogLevel.Information, "Listing {Count} pending invoices for the payment run")]
    static partial void LogListing(ILogger logger, int count);
}
