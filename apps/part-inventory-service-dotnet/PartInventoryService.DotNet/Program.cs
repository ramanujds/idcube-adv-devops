using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PartInventoryService.DotNet.Data;
using PartInventoryService.DotNet.HealthChecks;
using PartInventoryService.DotNet.Repositories;

var builder = WebApplication.CreateBuilder(args);

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

