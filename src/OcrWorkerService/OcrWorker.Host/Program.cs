using OcrWorker.Application;
using OcrWorker.Domain.Ports;
using OcrWorker.Host;
using OcrWorker.Infrastructure.Messaging;
using OcrWorker.Infrastructure.Ocr;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// Health checks (12-factor: expose health for orchestrators)
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks();

// ---------------------------------------------------------------------------
// OCR -- Mock adapter for development (swap for real adapter in production)
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IOcrPort, MockOcrAdapter>();
builder.Services.AddTransient<ProcessDocumentHandler>();

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
