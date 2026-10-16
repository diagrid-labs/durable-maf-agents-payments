using System.Net;
using System.Net.Http.Json;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Payments;

// Typed HttpClient for the payment provider. The base address is resolved through Aspire
// service discovery ("payment-gateway").
public sealed class PaymentGatewayClient(HttpClient http)
{
    public async Task<PaymentReceipt> PayAsync(PaymentRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/payments")
        {
            Content = JsonContent.Create(new
            {
                request.InvoiceId,
                request.SupplierId,
                request.Amount,
                request.Currency,
                request.BankAccount
            })
        };
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey);

        using var response = await http.SendAsync(message, ct);

        // A business rejection (key reused for a different payment) is returned as data:
        // retrying it can never succeed, so it must not throw and trigger the retry policy.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var problem = await response.Content.ReadAsStringAsync(ct);
            return new PaymentReceipt(null, PaymentStatus.Rejected, Replayed: false, problem);
        }

        // Anything else that is not a success is treated as transient and throws, so the
        // workflow retry policy on PayInvoiceActivity schedules another attempt.
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GatewayPayment>(ct)
            ?? throw new InvalidOperationException("Empty response from payment gateway.");
        var replayed = response.Headers.TryGetValues("Idempotent-Replayed", out var values)
            && values.Contains("true");

        return new PaymentReceipt(body.PaymentId, body.Status, replayed, null);
    }

    private sealed record GatewayPayment(string PaymentId, string Status);
}
