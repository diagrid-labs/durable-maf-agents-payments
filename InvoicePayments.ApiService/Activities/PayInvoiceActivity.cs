using Dapr.Workflow;
using InvoicePayments.ApiService.Models;
using InvoicePayments.ApiService.Payments;

namespace InvoicePayments.ApiService.Activities;

// The one activity with a real-world side effect. Dapr Workflow runs activities at least once,
// so this body can run again with the same input: after a crash, a timeout or a retry. The
// idempotency key in the request (derived in the workflow from the invoice) makes the payment
// provider return the original payment instead of charging twice.
internal sealed partial class PayInvoiceActivity(
    PaymentGatewayClient gateway,
    ILogger<PayInvoiceActivity> logger)
    : WorkflowActivity<PaymentRequest, PaymentReceipt>
{
    private static int _acceptedPayments;

    private static readonly bool SimulateCrash =
        string.Equals(Environment.GetEnvironmentVariable("SIMULATE_CRASH"), "true", StringComparison.OrdinalIgnoreCase);

    public override async Task<PaymentReceipt> RunAsync(WorkflowActivityContext context, PaymentRequest request)
    {
        var receipt = await gateway.PayAsync(request);

        if (receipt.Replayed)
            LogReplayed(logger, request.InvoiceId, receipt.PaymentId, request.IdempotencyKey);
        else
            LogPaid(logger, request.InvoiceId, receipt.PaymentId, receipt.Status);

        // 💥 DURABILITY DEMO (opt-in): with SIMULATE_CRASH=true, crash right after the provider
        // accepted the second new payment, before this activity's result is checkpointed. Restart
        // the app without SIMULATE_CRASH: the workflow resumes, re-runs this activity for the
        // in-flight invoices, and the provider answers with the original payment.
        if (SimulateCrash && !receipt.Replayed && Interlocked.Increment(ref _acceptedPayments) == 2)
            Environment.FailFast("Simulated crash after the payment was accepted.");

        return receipt;
    }

    [LoggerMessage(LogLevel.Information, "💸 Paid {InvoiceId}: {PaymentId} ({Status})")]
    static partial void LogPaid(ILogger logger, string invoiceId, string? paymentId, string status);

    [LoggerMessage(LogLevel.Warning, "♻️ {InvoiceId} was already paid as {PaymentId} (idempotency key {Key}), no new charge")]
    static partial void LogReplayed(ILogger logger, string invoiceId, string? paymentId, string key);
}
