using System.Globalization;
using Microsoft.Data.Sqlite;
using PartInventoryService.DotNet.Data;
using PartInventoryService.DotNet.Models;

namespace PartInventoryService.DotNet.Repositories;

public class PartRepository : IPartRepository
{
	private readonly SqliteConnection _connection;
	private readonly Lock _sync = new();

	public PartRepository(InventoryDatabase database)
	{
		_connection = database.Connection;
	}

	public Part Create(Part part)
	{
		lock (_sync)
		{
			using var command = _connection.CreateCommand();
			command.CommandText = "INSERT INTO parts (id, sku, name, price, stock) VALUES ($id, $sku, $name, $price, $stock)";
			command.Parameters.AddWithValue("$id", part.Id);
			command.Parameters.AddWithValue("$sku", part.Sku);
			command.Parameters.AddWithValue("$name", part.Name);
			command.Parameters.AddWithValue("$price", part.Price);
			command.Parameters.AddWithValue("$stock", part.Stock);
			command.ExecuteNonQuery();

			return FindByIdCore(part.Id)!;
		}
	}

	public IReadOnlyList<Part> FindAll()
	{
		lock (_sync)
		{
			using var command = _connection.CreateCommand();
			command.CommandText = "SELECT id, sku, name, price, stock FROM parts ORDER BY rowid";
			using var reader = command.ExecuteReader();

			var parts = new List<Part>();
			while (reader.Read())
			{
				parts.Add(MapPart(reader));
			}

			return parts;
		}
	}

	public Part? FindById(string id)
	{
		lock (_sync)
		{
			return FindByIdCore(id);
		}
	}

	public IReadOnlyList<Part> FindBySku(string sku)
	{
		lock (_sync)
		{
			using var command = _connection.CreateCommand();
			command.CommandText = "SELECT id, sku, name, price, stock FROM parts WHERE sku = $sku ORDER BY rowid";
			command.Parameters.AddWithValue("$sku", sku);
			using var reader = command.ExecuteReader();

			var parts = new List<Part>();
			while (reader.Read())
			{
				parts.Add(MapPart(reader));
			}

			return parts;
		}
	}

	public Part? Update(string id, PartInputModel partDetails)
	{
		lock (_sync)
		{
			using var command = _connection.CreateCommand();
			command.CommandText = "UPDATE parts SET sku = $sku, name = $name, price = $price, stock = $stock WHERE id = $id";
			command.Parameters.AddWithValue("$id", id);
			command.Parameters.AddWithValue("$sku", partDetails.Sku);
			command.Parameters.AddWithValue("$name", partDetails.Name);
			command.Parameters.AddWithValue("$price", partDetails.Price);
			command.Parameters.AddWithValue("$stock", partDetails.Stock);
			command.ExecuteNonQuery();

			return FindByIdCore(id);
		}
	}

	public bool DeleteById(string id)
	{
		lock (_sync)
		{
			using var command = _connection.CreateCommand();
			command.CommandText = "DELETE FROM parts WHERE id = $id";
			command.Parameters.AddWithValue("$id", id);
			return command.ExecuteNonQuery() > 0;
		}
	}

	public bool ExistsById(string id)
	{
		lock (_sync)
		{
			using var command = _connection.CreateCommand();
			command.CommandText = "SELECT 1 FROM parts WHERE id = $id LIMIT 1";
			command.Parameters.AddWithValue("$id", id);
			return command.ExecuteScalar() is not null;
		}
	}

	public Part? DecrementStock(string id, int quantity)
	{
		lock (_sync)
		{
			using var command = _connection.CreateCommand();
			command.CommandText = "UPDATE parts SET stock = stock - $quantity WHERE id = $id";
			command.Parameters.AddWithValue("$quantity", quantity);
			command.Parameters.AddWithValue("$id", id);
			command.ExecuteNonQuery();

			return FindByIdCore(id);
		}
	}

	private Part? FindByIdCore(string id)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = "SELECT id, sku, name, price, stock FROM parts WHERE id = $id";
		command.Parameters.AddWithValue("$id", id);
		using var reader = command.ExecuteReader();

		return reader.Read() ? MapPart(reader) : null;
	}

	private static Part MapPart(SqliteDataReader reader)
	{
		return new Part
		{
			Id = reader.GetString(0),
			Sku = reader.GetString(1),
			Name = reader.GetString(2),
			Price = decimal.Parse(reader.GetValue(3).ToString()!, CultureInfo.InvariantCulture),
			Stock = reader.GetInt32(4)
		};
	}
}

