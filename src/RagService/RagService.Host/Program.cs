using RagService.Host;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// Health checks
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks();

// ---------------------------------------------------------------------------
// Hosted services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<RagWorkerService>();

var host = builder.Build();
host.Run();
