using OcrWorker.Host;
using OcrWorker.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// Health checks (12-factor: expose health for orchestrators)
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks();

// ---------------------------------------------------------------------------
// Messaging -- RabbitMQ via MassTransit (12-factor: config from env)
// ---------------------------------------------------------------------------
builder.Services.AddOcrMessaging(builder.Configuration);

// ---------------------------------------------------------------------------
// Hosted services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<OcrWorkerService>();

var host = builder.Build();
host.Run();
