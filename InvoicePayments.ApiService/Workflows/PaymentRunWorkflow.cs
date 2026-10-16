using Dapr.Workflow;
using Diagrid.AI.Microsoft.AgentFramework.Abstractions;
using Diagrid.AI.Microsoft.AgentFramework.Runtime;
using InvoicePayments.ApiService.Activities;
using InvoicePayments.ApiService.Agents;
using InvoicePayments.ApiService.Checks;
using InvoicePayments.ApiService.Models;
using InvoicePayments.ApiService.Payments;

namespace InvoicePayments.ApiService.Workflows;

public sealed partial class PaymentRunWorkflow : Workflow<PaymentRunInput, PaymentRunOutput>
{
    // Transient provider failures (timeouts, 5xx) are retried with backoff. Retrying is only
    // safe because every attempt carries the same idempotency key.
    private static readonly WorkflowTaskOptions PaymentRetry = new(
        new WorkflowRetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(2),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromSeconds(30)));

    public override async Task<PaymentRunOutput> RunAsync(WorkflowContext context, PaymentRunInput input)
    {
        var logger = context.CreateReplaySafeLogger<PaymentRunWorkflow>();
        LogStart(logger, context.InstanceId, input.MaxInvoices);

        var invoices = await context.CallActivityAsync<IReadOnlyList<InvoiceSummary>>(
            nameof(ListPendingInvoicesActivity), new ListPendingInvoicesInput(input.MaxInvoices));

        var reviewer = context.GetAgent(AgentNames.InvoiceReviewer);

        // Durable fan-out: every invoice gets its own checkpointed checks, agent call and payment.
        var outcomes = await Task.WhenAll(
            invoices.Select(invoice => ProcessInvoiceAsync(context, reviewer, invoice, logger)));

        var summarizer = context.GetAgent(AgentNames.RunSummarizer);
        RunSummary? summary = null;
        try
        {
            summary = await context.RunAgentAndDeserializeAsync<RunSummary>(
                agent: summarizer,
                logger: logger,
                message: Prompts.RunSummary(outcomes));
        }
        catch (Exception ex)
        {
            LogSummaryDegraded(logger, ex.Message);
        }

        var summaryText = summary?.Summary ?? "Summary unavailable.";
        var report = await context.CallActivityAsync<WriteReportOutput>(
            nameof(WritePaymentReportActivity),
            new WriteReportInput(context.InstanceId, summaryText, outcomes));

        var paid = outcomes.Where(o => o.Paid).ToList();
        LogDone(logger, context.InstanceId, paid.Count, outcomes.Length);

        return new PaymentRunOutput(
            context.InstanceId,
            outcomes.Length,
            paid.Count,
            outcomes.Length - paid.Count,
            paid.Sum(o => o.Amount),
            report.Path,
            summaryText);
    }

    private static async Task<InvoiceOutcome> ProcessInvoiceAsync(
        WorkflowContext context, IDaprAIAgent reviewer, InvoiceSummary invoice, ILogger logger)
    {
        var invoiceCase = await context.CallActivityAsync<InvoiceCase>(
            nameof(GetInvoiceCaseActivity), new GetInvoiceCaseInput(invoice.InvoiceId));

        // The agent call runs as a checkpointed workflow activity. After a crash its result is
        // read from history, so the LLM is not called again for this invoice.
        InvoiceReview? review = null;
        try
        {
            LogReviewing(logger, invoice.InvoiceId);
            review = await context.RunAgentAndDeserializeAsync<InvoiceReview>(
                agent: reviewer,
                logger: logger,
                message: Prompts.Review(invoiceCase));
        }
        catch (Exception ex)
        {
            // One failed agent call must not fail the whole run: hold this invoice instead.
            LogReviewDegraded(logger, invoice.InvoiceId, ex.Message);
        }

        await context.CallActivityAsync<RecordAgentCallResult>(
            nameof(RecordAgentCallActivity), new AgentCallRecord(invoice.InvoiceId, invoice.SupplierName));

        // Deterministic decision in workflow code: same inputs, same decision, on every replay.
        var decision = PaymentPolicy.Decide(invoiceCase.Checks, review);
        if (!decision.Approved)
        {
            LogHeld(logger, invoice.InvoiceId, decision.Reason);
            return new InvoiceOutcome(invoice.InvoiceId, invoice.SupplierName, invoice.Amount, invoice.Currency,
                invoiceCase.Checks, review, decision, Receipt: null);
        }

        // The idempotency key is computed here, from the invoice, and passed in as activity
        // input. It is identical on every attempt and every replay of this payment.
        var request = new PaymentRequest(
            IdempotencyKey: IdempotencyKeys.ForInvoice(invoiceCase.Invoice),
            InvoiceId: invoiceCase.Invoice.InvoiceId,
            SupplierId: invoiceCase.Invoice.SupplierId,
            Amount: invoiceCase.Invoice.Amount,
            Currency: invoiceCase.Invoice.Currency,
            BankAccount: invoiceCase.Invoice.BankAccount);

        var receipt = await context.CallActivityAsync<PaymentReceipt>(
            nameof(PayInvoiceActivity), request, PaymentRetry);

        var final = receipt.Status == PaymentStatus.Accepted
            ? decision
            : new PaymentDecision(false, $"Payment rejected by provider: {receipt.Message}");

        return new InvoiceOutcome(invoice.InvoiceId, invoice.SupplierName, invoice.Amount, invoice.Currency,
            invoiceCase.Checks, review, final, receipt);
    }

    [LoggerMessage(LogLevel.Information, "Starting payment run {InstanceId} (max {MaxInvoices} invoices)")]
    static partial void LogStart(ILogger logger, string instanceId, int maxInvoices);

    [LoggerMessage(LogLevel.Information, "🤖 Reviewing {InvoiceId} with the InvoiceReviewer agent")]
    static partial void LogReviewing(ILogger logger, string invoiceId);

    [LoggerMessage(LogLevel.Warning, "Review degraded for {InvoiceId}: {Reason}")]
    static partial void LogReviewDegraded(ILogger logger, string invoiceId, string reason);

    [LoggerMessage(LogLevel.Information, "✋ Holding {InvoiceId}: {Reason}")]
    static partial void LogHeld(ILogger logger, string invoiceId, string reason);

    [LoggerMessage(LogLevel.Warning, "Run summary degraded: {Reason}")]
    static partial void LogSummaryDegraded(ILogger logger, string reason);

    [LoggerMessage(LogLevel.Information, "Completed payment run {InstanceId}: paid {Paid} of {Count} invoices")]
    static partial void LogDone(ILogger logger, string instanceId, int paid, int count);
}
