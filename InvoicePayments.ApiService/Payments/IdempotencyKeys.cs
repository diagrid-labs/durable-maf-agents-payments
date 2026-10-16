using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Payments;

public static class IdempotencyKeys
{
    // Derived from the business identity of the payment, not from a Guid or a timestamp.
    // Every retry, every replay and even a second payment run for the same invoice produce
    // the same key, so the provider can refuse to pay the same invoice twice.
    public static string ForInvoice(Invoice invoice) =>
        $"pay-{invoice.SupplierId}-{invoice.InvoiceId}".ToLowerInvariant();
}
