using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PartInventoryService.DotNet.Data;
using PartInventoryService.DotNet.HealthChecks;
using PartInventoryService.DotNet.Repositories;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "/app/logs/app.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<InventoryDatabase>();
builder.Services.AddSingleton<IPartRepository, PartRepository>();
builder.Services
  .AddHealthChecks()
  .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
  .AddCheck<InventoryDatabaseHealthCheck>("inventory-database", tags: ["ready"]);

var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

_ = app.Services.GetRequiredService<InventoryDatabase>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
  Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
  Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;

