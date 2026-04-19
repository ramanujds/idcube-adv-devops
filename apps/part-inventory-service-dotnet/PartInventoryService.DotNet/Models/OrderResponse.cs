namespace PartInventoryService.DotNet.Models;

public class OrderResponse
{
	public string PartSku { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public int Quantity { get; set; }

	public decimal TotalPrice { get; set; }
}

