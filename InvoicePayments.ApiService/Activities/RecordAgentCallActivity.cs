using Dapr.Workflow;
using InvoicePayments.ApiService.Demo;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Activities;

// Durably records that one InvoiceReviewer agent call executed, by appending to the
// agent-call ledger. Because this runs as a checkpointed workflow activity, a completed
// record is replayed from history (not re-executed) after a crash, so the ledger ends up
// with each invoice exactly once: proof that LLM calls were not repeated on resume.
internal sealed partial class RecordAgentCallActivity(ILogger<RecordAgentCallActivity> logger)
    : WorkflowActivity<AgentCallRecord, RecordAgentCallResult>
{
    public override Task<RecordAgentCallResult> RunAsync(WorkflowActivityContext context, AgentCallRecord record)
    {
        new AgentCallLedger(OutputPaths.OutputDirectory())
            .Append(record.InvoiceId, record.SupplierName, DateTime.UtcNow);

        LogRecorded(logger, record.InvoiceId);
        return Task.FromResult(new RecordAgentCallResult(true));
    }

    [LoggerMessage(LogLevel.Information, "📒 Recorded agent call for {InvoiceId}")]
    static partial void LogRecorded(ILogger logger, string invoiceId);
}
