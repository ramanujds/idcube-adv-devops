using PartInventoryService.DotNet.Models;

namespace PartInventoryService.DotNet.Repositories;

public interface IPartRepository
{
	Part Create(Part part);

	IReadOnlyList<Part> FindAll();

	Part? FindById(string id);

	IReadOnlyList<Part> FindBySku(string sku);

	Part? Update(string id, PartInputModel partDetails);

	bool DeleteById(string id);

	bool ExistsById(string id);

	Part? DecrementStock(string id, int quantity);
}

