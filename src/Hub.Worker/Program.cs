using Hub.Infrastructure;
using Hub.Worker.Workers;


var builder = Host.CreateApplicationBuilder(args);
builder.AddHubInfrastructure("atlas-document-worker");
builder.Services.AddHostedService<DocumentWorker>();
await builder.Build().RunAsync();
