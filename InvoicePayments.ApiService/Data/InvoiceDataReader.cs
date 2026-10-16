using System.Text.Json;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Data;

public sealed class InvoiceDataReader(string dataDir)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private string InvoicesDir => Path.Combine(dataDir, "invoices");
    private string PurchaseOrdersDir => Path.Combine(dataDir, "purchase-orders");

    public IReadOnlyList<string> ListInvoiceIds(int max)
    {
        if (!Directory.Exists(InvoicesDir))
            return [];

        return Directory.EnumerateFiles(InvoicesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .Take(max)
            .ToList();
    }

    public Invoice GetInvoice(string invoiceId) =>
        Read<Invoice>(Path.Combine(InvoicesDir, $"{invoiceId}.json"))
        ?? throw new FileNotFoundException($"Invoice {invoiceId} not found in {InvoicesDir}");

    public PurchaseOrder? FindPurchaseOrder(string? poNumber) =>
        string.IsNullOrWhiteSpace(poNumber)
            ? null
            : Read<PurchaseOrder>(Path.Combine(PurchaseOrdersDir, $"{poNumber}.json"));

    private static T? Read<T>(string path) where T : class =>
        File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;
}
