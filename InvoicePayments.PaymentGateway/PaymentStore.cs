using System.Text.Json;

namespace InvoicePayments.PaymentGateway;

public record PaymentBody(string InvoiceId, string SupplierId, decimal Amount, string Currency, string BankAccount);

public record StoredPayment(
    string PaymentId,
    string IdempotencyKey,
    string Status,
    PaymentBody Request,
    DateTime CreatedUtc);

public enum CreateOutcome { Created, Replayed, KeyReusedWithDifferentRequest }

// A stand-in for a payment provider's ledger, persisted to a JSON file so it survives the
// crash-and-restart demo. The idempotency key is the unique constraint: creating a payment
// is one conditional operation under a lock, never "check, then write" in two steps.
public sealed class PaymentStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Lock _lock = new();

    public (CreateOutcome Outcome, StoredPayment Payment) Create(string idempotencyKey, PaymentBody request)
    {
        lock (_lock)
        {
            var payments = Load();

            if (payments.TryGetValue(idempotencyKey, out var existing))
            {
                var outcome = existing.Request == request
                    ? CreateOutcome.Replayed
                    : CreateOutcome.KeyReusedWithDifferentRequest;
                return (outcome, existing);
            }

            var payment = new StoredPayment(
                PaymentId: $"pay_{Guid.NewGuid():N}"[..16],
                IdempotencyKey: idempotencyKey,
                Status: "accepted",
                Request: request,
                CreatedUtc: DateTime.UtcNow);

            payments[idempotencyKey] = payment;
            Save(payments);
            return (CreateOutcome.Created, payment);
        }
    }

    public IReadOnlyList<StoredPayment> List()
    {
        lock (_lock)
            return Load().Values.OrderBy(p => p.CreatedUtc).ToList();
    }

    public void Clear()
    {
        lock (_lock)
            Save([]);
    }

    private Dictionary<string, StoredPayment> Load() =>
        File.Exists(filePath)
            ? JsonSerializer.Deserialize<List<StoredPayment>>(File.ReadAllText(filePath), Options)!
                .ToDictionary(p => p.IdempotencyKey)
            : [];

    // Write to a temp file and move it into place, so a crash never leaves a half-written ledger.
    private void Save(Dictionary<string, StoredPayment> payments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var tmp = filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(payments.Values.OrderBy(p => p.CreatedUtc), Options));
        File.Move(tmp, filePath, overwrite: true);
    }
}
