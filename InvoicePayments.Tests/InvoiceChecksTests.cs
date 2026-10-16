using InvoicePayments.ApiService.Checks;
using InvoicePayments.ApiService.Data;

namespace InvoicePayments.Tests;

// Runs the deterministic checks against the real fixtures in /data, so the expected outcome
// of the demo payment run is pinned down by tests.
public class InvoiceChecksTests
{
    private static readonly InvoiceDataReader Reader = new(TestPaths.DataDir);

    [Theory]
    [InlineData("INV-1001")]
    [InlineData("INV-1002")]
    [InlineData("INV-1006")]
    [InlineData("INV-1007")] // 0.87% over the PO, within tolerance
    [InlineData("INV-1008")] // passes the rules; only the agent can catch the gift cards
    public void Clean_invoices_pass(string invoiceId)
    {
        var invoice = Reader.GetInvoice(invoiceId);
        Assert.True(InvoiceChecks.Run(invoice, Reader.FindPurchaseOrder(invoice.PoNumber)).Passed);
    }

    [Theory]
    [InlineData("INV-1003", "amount-over-po")]
    [InlineData("INV-1004", "no-purchase-order")]
    [InlineData("INV-1005", "bank-account-changed")]
    public void Risky_invoices_are_flagged(string invoiceId, string expectedFlag)
    {
        var invoice = Reader.GetInvoice(invoiceId);
        var result = InvoiceChecks.Run(invoice, Reader.FindPurchaseOrder(invoice.PoNumber));
        Assert.Contains(expectedFlag, result.Flags);
    }

    [Fact]
    public void Line_total_mismatch_is_flagged()
    {
        var invoice = Reader.GetInvoice("INV-1001") with { Amount = 1300m };
        var result = InvoiceChecks.Run(invoice, Reader.FindPurchaseOrder(invoice.PoNumber));
        Assert.Contains("line-total-mismatch", result.Flags);
    }

    [Fact]
    public void Lists_all_fixture_invoices_in_order()
    {
        var ids = Reader.ListInvoiceIds(100);
        Assert.Equal(8, ids.Count);
        Assert.Equal("INV-1001", ids[0]);
        Assert.Equal("INV-1008", ids[^1]);
    }
}
