using System.Text.Json.Serialization;

namespace InvoicePayments.ApiService.Models;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InvoiceReview))]
[JsonSerializable(typeof(RunSummary))]
public partial class InvoicePaymentsJsonContext : JsonSerializerContext;
