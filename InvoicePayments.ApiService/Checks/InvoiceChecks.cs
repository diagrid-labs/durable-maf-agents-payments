using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Checks;

// Deterministic three-way-match style checks. These run in code, not in the LLM: a model
// can add caution on top of them, but it can never approve a payment these checks block.
public static class InvoiceChecks
{
    // Invoices may exceed the approved PO amount by at most 2% (rounding, small shipping costs).
    public const decimal OverPoTolerance = 0.02m;

    public static CheckResult Run(Invoice invoice, PurchaseOrder? po)
    {
        var flags = new List<string>();

        var lineTotal = invoice.Lines.Sum(l => l.Quantity * l.UnitPrice);
        if (lineTotal != invoice.Amount)
            flags.Add("line-total-mismatch");

        if (po is null)
        {
            flags.Add("no-purchase-order");
            return new CheckResult(flags);
        }

        if (po.SupplierId != invoice.SupplierId)
            flags.Add("supplier-mismatch");

        if (po.Currency != invoice.Currency)
            flags.Add("currency-mismatch");

        if (invoice.Amount > po.ApprovedAmount * (1 + OverPoTolerance))
            flags.Add("amount-over-po");

        if (po.BankAccountOnFile != invoice.BankAccount)
            flags.Add("bank-account-changed");

        return new CheckResult(flags);
    }
}
