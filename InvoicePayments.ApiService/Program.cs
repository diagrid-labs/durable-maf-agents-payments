using Microsoft.AspNetCore.Mvc;
using Dapr.Workflow;
using Diagrid.AI.Microsoft.AgentFramework.Hosting;
using InvoicePayments.ApiService.Agents;
using InvoicePayments.ApiService.Data;
using InvoicePayments.ApiService.Models;
using InvoicePayments.ApiService.Payments;
using InvoicePayments.ApiService.Workflows;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var dataDir = Environment.GetEnvironmentVariable("DATA_DIR")
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "data");
builder.Services.AddSingleton(new InvoiceDataReader(Path.GetFullPath(dataDir)));

// The payment provider, resolved through Aspire service discovery.
builder.Services.AddHttpClient<PaymentGatewayClient>(c => c.BaseAddress = new Uri("https+http://payment-gateway"));

// Microsoft Agent Framework agents, hosted by Dapr Agents. Each agent call made from a
// workflow runs as a Dapr Workflow activity, so its result is checkpointed. The model and
// the OpenAI key are owned by the Dapr conversation component (conversation-openai).
builder.Services.AddDaprAgents(
        opt => opt.AddContext(() => InvoicePaymentsJsonContext.Default))
    .WithAgent(
        agentName: AgentNames.InvoiceReviewer,
        conversationComponentName: "conversation-openai",
        instructions: AgentInstructions.InvoiceReviewer,
        serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent(
        agentName: AgentNames.RunSummarizer,
        conversationComponentName: "conversation-openai",
        instructions: AgentInstructions.RunSummarizer,
        serviceLifetime: ServiceLifetime.Singleton);

var app = builder.Build();

app.MapPost("/payment-runs", async (
    [FromServices] DaprWorkflowClient workflowClient,
    [FromBody] PaymentRunInput input) =>
{
    var instanceId = await workflowClient.ScheduleNewWorkflowAsync(
        name: nameof(PaymentRunWorkflow),
        instanceId: input.Id,
        input: input);
    return Results.Accepted($"/payment-runs/{instanceId}", new { instanceId });
});

app.MapGet("/payment-runs/{instanceId}", async (
    [FromRoute] string instanceId,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    if (state is null || !state.Exists)
        return Results.NotFound($"Payment run '{instanceId}' not found.");

    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        output = state.ReadOutputAs<PaymentRunOutput>()
    });
});

app.MapPost("/payment-runs/{instanceId}/terminate", async (
    [FromRoute] string instanceId, [FromServices] DaprWorkflowClient c) =>
{ await c.TerminateWorkflowAsync(instanceId); return Results.Accepted(); });

app.MapPost("/payment-runs/{instanceId}/pause", async (
    [FromRoute] string instanceId, [FromServices] DaprWorkflowClient c) =>
{ await c.SuspendWorkflowAsync(instanceId); return Results.Accepted(); });

app.MapPost("/payment-runs/{instanceId}/resume", async (
    [FromRoute] string instanceId, [FromServices] DaprWorkflowClient c) =>
{ await c.ResumeWorkflowAsync(instanceId); return Results.Accepted(); });

app.MapDefaultEndpoints();
app.Run();
