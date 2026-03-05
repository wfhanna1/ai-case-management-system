using RagService.Host;
using RagService.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// Health checks
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks();

// ---------------------------------------------------------------------------
// Messaging -- RabbitMQ via MassTransit (12-factor: config from env)
// ---------------------------------------------------------------------------
builder.Services.AddRagMessaging(builder.Configuration);

// ---------------------------------------------------------------------------
// Hosted services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<RagWorkerService>();

var host = builder.Build();
host.Run();
