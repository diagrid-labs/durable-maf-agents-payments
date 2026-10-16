using InvoicePayments.PaymentGateway;

// A mock payment provider that behaves like real ones (Stripe, Adyen, PayPal) do with an
// Idempotency-Key header: the first request creates the payment, a repeat of the same request
// returns the original payment, and reusing a key for a different payment is refused.
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var outputDir = Environment.GetEnvironmentVariable("OUTPUT_DIR")
    ?? Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName, "out");
builder.Services.AddSingleton(new PaymentStore(Path.Combine(outputDir, "payments.json")));

var app = builder.Build();

app.MapPost("/payments", (HttpContext http, PaymentBody body, PaymentStore store, ILogger<Program> logger) =>
{
    if (!http.Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
        return Results.BadRequest("An Idempotency-Key header is required.");

    var (outcome, payment) = store.Create(key!, body);
    switch (outcome)
    {
        case CreateOutcome.Created:
            logger.LogInformation("New payment {PaymentId} for {InvoiceId}: {Amount} {Currency}",
                payment.PaymentId, body.InvoiceId, body.Amount, body.Currency);
            return Results.Created($"/payments/{payment.PaymentId}", new { payment.PaymentId, payment.Status });

        case CreateOutcome.Replayed:
            logger.LogWarning("Replayed idempotency key {Key}, returning existing payment {PaymentId}",
                key.ToString(), payment.PaymentId);
            http.Response.Headers["Idempotent-Replayed"] = "true";
            return Results.Ok(new { payment.PaymentId, payment.Status });

        default:
            return Results.UnprocessableEntity(
                $"Idempotency key '{key}' was already used for a different payment.");
    }
});

app.MapGet("/payments", (PaymentStore store) => Results.Ok(store.List()));

// Demo helper: forget all payments so the next payment run starts clean.
app.MapDelete("/payments", (PaymentStore store) => { store.Clear(); return Results.NoContent(); });

app.MapDefaultEndpoints();
app.Run();
