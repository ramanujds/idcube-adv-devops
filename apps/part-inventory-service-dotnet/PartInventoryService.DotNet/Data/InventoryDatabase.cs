using Microsoft.Data.Sqlite;

namespace PartInventoryService.DotNet.Data;

public sealed class InventoryDatabase : IDisposable
{
	private readonly IWebHostEnvironment _environment;

	public InventoryDatabase(IWebHostEnvironment environment)
	{
		_environment = environment;
		Connection = new SqliteConnection("Data Source=:memory:");
		Connection.Open();

		ExecuteScript("schema.sql");
		ExecuteScript("data.sql");
	}

	public SqliteConnection Connection { get; }

	public void Dispose()
	{
		Connection.Dispose();
	}

	private void ExecuteScript(string fileName)
	{
		var path = Path.Combine(_environment.ContentRootPath, "resources", fileName);
		var sql = File.ReadAllText(path);

		using var command = Connection.CreateCommand();
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}
}

