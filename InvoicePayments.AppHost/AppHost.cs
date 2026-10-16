using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Absolute paths so the services never depend on the working directory Aspire assigns.
var dataDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "data"));
var outputDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "out"));

// Catalyst mode: when the Catalyst connection values are present in the environment that
// launches the AppHost, the API talks to a Catalyst-managed Dapr sidecar instead of a local one.
var catalystGrpc = builder.Configuration["DAPR_GRPC_ENDPOINT"];
var useCatalyst = !string.IsNullOrWhiteSpace(catalystGrpc);

// A mock payment provider that honours the Idempotency-Key header.
var gateway = builder.AddProject<Projects.InvoicePayments_PaymentGateway>("payment-gateway")
    .WithEnvironment("OUTPUT_DIR", outputDir)
    .WithEndpoint("http", endpoint => endpoint.Port = 5091);

var api = builder.AddProject<Projects.InvoicePayments_ApiService>("invoice-payments")
    .WithReference(gateway)
    .WaitFor(gateway)
    .WithEnvironment("DATA_DIR", dataDir)
    .WithEnvironment("OUTPUT_DIR", outputDir)
    // Pin the API's HTTP endpoint so the README URLs are stable.
    .WithEndpoint("http", endpoint => endpoint.Port = 5090);

if (useCatalyst)
{
    api.WithEnvironment("DAPR_GRPC_ENDPOINT", catalystGrpc)
       .WithEnvironment("DAPR_HTTP_ENDPOINT", builder.Configuration["DAPR_HTTP_ENDPOINT"])
       .WithEnvironment("DAPR_API_TOKEN", builder.Configuration["DAPR_API_TOKEN"]);
}
else
{
    // Local mode: Aspire owns a Valkey container as the workflow state store. Port 16379 and
    // the password match resources/statestore.yaml. The data volume keeps workflow state
    // across restarts, which is what makes the crash-and-resume demo work.
    var statePassword = builder.AddParameter("state-password", "state-store-123", secret: true);
    var stateStore = builder
        .AddValkey("statestore", 16379, statePassword)
        .WithContainerName("invoice-payments-state")
        .WithDataVolume("invoice-payments-state-data");

    api.WithReference(stateStore)
       .WaitFor(stateStore)
       .WithDaprSidecar(new DaprSidecarOptions
       {
           AppId = "invoice-payments",
           ResourcesPaths = ["resources"]
       });

    // The Diagrid Dapr Dev Dashboard shows the workflow runs, sidecars and components.
    builder.AddExecutable("dev-dashboard", "diagrid-dev-dashboard", ".",
            "--port", "9090", "--no-open")
        .WithHttpEndpoint(port: 9090, isProxied: false);
}

builder.Build().Run();
