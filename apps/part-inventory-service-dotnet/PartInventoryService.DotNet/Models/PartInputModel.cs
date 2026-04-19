namespace PartInventoryService.DotNet.Models;

public class PartInputModel
{
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }
}

