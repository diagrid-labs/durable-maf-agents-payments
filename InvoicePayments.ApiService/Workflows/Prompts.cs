using System.Globalization;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Workflows;

// Prompt builders are pure functions of their input, so calling them from workflow code is
// replay-safe: the same history always produces the same prompt.
internal static class Prompts
{
    public static string Review(InvoiceCase c)
    {
        var i = c.Invoice;
        var lines = string.Join("\n", i.Lines.Select(l =>
            $"- {l.Description}: {l.Quantity} x {Money(l.UnitPrice)}"));
        var po = c.PurchaseOrder is { } p
            ? $"{p.PoNumber}, \"{p.Description}\", approved {Money(p.ApprovedAmount)} {p.Currency}"
            : "none";
        var flags = c.Checks.Passed ? "none" : string.Join(", ", c.Checks.Flags);

        return
            $"Review invoice {i.InvoiceId} from {i.SupplierName} ({i.SupplierId}).\n" +
            $"Amount: {Money(i.Amount)} {i.Currency}, due {i.DueDate:yyyy-MM-dd}.\n" +
            $"Purchase order: {po}.\n" +
            $"Lines:\n{lines}\n" +
            $"Notes: {i.Notes ?? "none"}\n" +
            $"Deterministic check flags: {flags}.\n" +
            "Return strict JSON {\"recommendation\": \"pay\"|\"hold\", \"summary\": string, \"rationale\": string}.";
    }

    public static string RunSummary(IReadOnlyList<InvoiceOutcome> outcomes)
    {
        var rows = outcomes.Select(o =>
            $"[{o.InvoiceId} supplier=\"{o.SupplierName}\" amount={Money(o.Amount)} {o.Currency} " +
            $"result={(o.Paid ? "paid" : "held")} reason=\"{o.Decision.Reason}\"]");
        return "Payment run outcomes: " + string.Join(" ", rows) +
               " Write a 2-3 sentence summary. Return strict JSON: {\"summary\": string}.";
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
