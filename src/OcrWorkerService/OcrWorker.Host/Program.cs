using OcrWorker.Host;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// Health checks (12-factor: expose health for orchestrators)
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks();

// ---------------------------------------------------------------------------
// Hosted services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<OcrWorkerService>();

var host = builder.Build();
host.Run();
