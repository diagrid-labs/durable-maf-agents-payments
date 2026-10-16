using System.Globalization;
using System.Text;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Report;

public static class PaymentReportWriter
{
    public static string Render(string runId, string summary, IReadOnlyList<InvoiceOutcome> outcomes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Payment run {runId}");
        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine("| Invoice | Supplier | Amount | Result | Payment | Reason |");
        sb.AppendLine("|---|---|---:|---|---|---|");

        foreach (var o in outcomes.OrderBy(o => o.InvoiceId, StringComparer.Ordinal))
        {
            var result = o.Paid ? "Paid" : o.Receipt?.Status == PaymentStatus.Rejected ? "Rejected" : "Held";
            var payment = o.Receipt?.PaymentId ?? "";
            sb.AppendLine(
                $"| {o.InvoiceId} | {Escape(o.SupplierName)} | {o.Amount.ToString("N2", CultureInfo.InvariantCulture)} {o.Currency} " +
                $"| {result} | {payment} | {Escape(o.Decision.Reason)} |");
        }

        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|").ReplaceLineEndings(" ");
}
