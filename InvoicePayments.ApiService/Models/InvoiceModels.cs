using System.Text.Json.Serialization;

namespace InvoicePayments.ApiService.Models;

// ----- Input contract: invoice and purchase-order JSON on disk (data/invoices, data/purchase-orders) -----
public record InvoiceLine(string Description, int Quantity, decimal UnitPrice);

public record Invoice(
    string InvoiceId,
    string SupplierId,
    string SupplierName,
    string? PoNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string Currency,
    decimal Amount,
    string BankAccount,
    IReadOnlyList<InvoiceLine> Lines,
    string? Notes);

public record PurchaseOrder(
    string PoNumber,
    string SupplierId,
    decimal ApprovedAmount,
    string Currency,
    string BankAccountOnFile,
    string Description);

// ----- Deterministic, computed-in-code values -----
public record CheckResult(IReadOnlyList<string> Flags)
{
    public bool Passed => Flags.Count == 0;
}

public record InvoiceSummary(string InvoiceId, string SupplierName, decimal Amount, string Currency);

public record InvoiceCase(Invoice Invoice, PurchaseOrder? PurchaseOrder, CheckResult Checks);

// ----- Agent outputs (prose and a recommendation only, never the payment itself) -----
public record InvoiceReview(
    [property: JsonPropertyName("recommendation")] string Recommendation,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("rationale")] string Rationale);

public record RunSummary(
    [property: JsonPropertyName("summary")] string Summary);

// ----- Carried through the workflow -----
public record PaymentDecision(bool Approved, string Reason);

public record PaymentRequest(
    string IdempotencyKey,
    string InvoiceId,
    string SupplierId,
    decimal Amount,
    string Currency,
    string BankAccount);

public record PaymentReceipt(string? PaymentId, string Status, bool Replayed, string? Message);

public record InvoiceOutcome(
    string InvoiceId,
    string SupplierName,
    decimal Amount,
    string Currency,
    CheckResult Checks,
    InvoiceReview? Review,
    PaymentDecision Decision,
    PaymentReceipt? Receipt)
{
    public bool Paid => Receipt?.Status == PaymentStatus.Accepted;
}

public static class PaymentStatus
{
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
}

// ----- Workflow / activity IO -----
public record PaymentRunInput(string Id, int MaxInvoices = 10);

public record PaymentRunOutput(
    string RunId,
    int InvoiceCount,
    int PaidCount,
    int HeldCount,
    decimal TotalPaid,
    string ReportPath,
    string Summary);

public record WriteReportInput(string RunId, string Summary, IReadOnlyList<InvoiceOutcome> Outcomes);

// Durability-demo signal: one record per executed InvoiceReviewer agent call, written to the
// agent-call ledger by RecordAgentCallActivity.
public record AgentCallRecord(string InvoiceId, string SupplierName);

public record ListPendingInvoicesInput(int MaxInvoices);

public record GetInvoiceCaseInput(string InvoiceId);

public record RecordAgentCallResult(bool Recorded);

public record WriteReportOutput(string Path);
