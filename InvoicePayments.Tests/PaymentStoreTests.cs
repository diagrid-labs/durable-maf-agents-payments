using InvoicePayments.PaymentGateway;

namespace InvoicePayments.Tests;

public class PaymentStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "invoice-payments-tests", Guid.NewGuid().ToString("n"));
    private string FilePath => Path.Combine(_dir, "payments.json");
    private static readonly PaymentBody Body = new("INV-1001", "SUP-NORTHWIND", 1240.00m, "USD", "US-021000021-4410");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Same_key_and_request_returns_the_original_payment()
    {
        var store = new PaymentStore(FilePath);
        var first = store.Create("key-1", Body);
        var second = store.Create("key-1", Body);

        Assert.Equal(CreateOutcome.Created, first.Outcome);
        Assert.Equal(CreateOutcome.Replayed, second.Outcome);
        Assert.Equal(first.Payment.PaymentId, second.Payment.PaymentId);
        Assert.Single(store.List());
    }

    [Fact]
    public void Same_key_with_a_different_request_is_refused()
    {
        var store = new PaymentStore(FilePath);
        store.Create("key-1", Body);
        var other = store.Create("key-1", Body with { Amount = 9999m });

        Assert.Equal(CreateOutcome.KeyReusedWithDifferentRequest, other.Outcome);
        Assert.Single(store.List());
    }

    [Fact]
    public void Payments_survive_a_restart_of_the_store()
    {
        new PaymentStore(FilePath).Create("key-1", Body);
        var afterRestart = new PaymentStore(FilePath).Create("key-1", Body);
        Assert.Equal(CreateOutcome.Replayed, afterRestart.Outcome);
    }

    [Fact]
    public void Concurrent_duplicates_create_exactly_one_payment()
    {
        var store = new PaymentStore(FilePath);
        var outcomes = new System.Collections.Concurrent.ConcurrentBag<CreateOutcome>();

        Parallel.For(0, 20, _ => outcomes.Add(store.Create("key-1", Body).Outcome));

        Assert.Equal(1, outcomes.Count(o => o == CreateOutcome.Created));
        Assert.Single(store.List());
    }
}
