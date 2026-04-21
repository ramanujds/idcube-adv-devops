using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PartInventoryService.DotNet.Data;

namespace PartInventoryService.DotNet.HealthChecks;

public sealed class InventoryDatabaseHealthCheck(InventoryDatabase database) : IHealthCheck
{
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		if (database.Connection.State != ConnectionState.Open)
		{
			return Task.FromResult(HealthCheckResult.Unhealthy("Inventory database connection is not open."));
		}

		using var command = database.Connection.CreateCommand();
		command.CommandText = "SELECT 1";
		var result = command.ExecuteScalar();

		return Task.FromResult(result is 1L or 1
			? HealthCheckResult.Healthy("Inventory database is ready.")
			: HealthCheckResult.Unhealthy("Inventory database readiness check returned an unexpected result."));
	}
}

