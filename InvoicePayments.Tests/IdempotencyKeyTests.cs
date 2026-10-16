using InvoicePayments.ApiService.Data;
using InvoicePayments.ApiService.Payments;

namespace InvoicePayments.Tests;

public class IdempotencyKeyTests
{
    private static readonly InvoiceDataReader Reader = new(TestPaths.DataDir);

    [Fact]
    public void Key_is_stable_for_the_same_invoice() =>
        Assert.Equal(
            IdempotencyKeys.ForInvoice(Reader.GetInvoice("INV-1001")),
            IdempotencyKeys.ForInvoice(Reader.GetInvoice("INV-1001")));

    [Fact]
    public void Key_differs_between_invoices() =>
        Assert.NotEqual(
            IdempotencyKeys.ForInvoice(Reader.GetInvoice("INV-1001")),
            IdempotencyKeys.ForInvoice(Reader.GetInvoice("INV-1002")));
}
