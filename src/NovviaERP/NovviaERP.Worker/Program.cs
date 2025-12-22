using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;

var builder = Host.CreateApplicationBuilder(args);

// Services registrieren
builder.Services.AddSingleton<JtlDbContext>();
builder.Services.AddSingleton<ZahlungsabgleichWorker>();
builder.Services.AddSingleton<WooCommerceSyncWorker>();
builder.Services.AddSingleton<MahnlaufWorker>();
builder.Services.AddSingleton<WorkflowQueueWorker>();

// Hosted Services (Worker)
builder.Services.AddHostedService<ZahlungsabgleichWorker>();
builder.Services.AddHostedService<WooCommerceSyncWorker>();
builder.Services.AddHostedService<MahnlaufWorker>();
builder.Services.AddHostedService<WorkflowQueueWorker>();

var host = builder.Build();
host.Run();
