namespace PartInventoryService.DotNet.Models;

public class InventoryUpdateViewModel
{
	public string Type { get; set; } = "success";

	public string Message { get; set; } = "Operation completed.";

	public string NextUrl { get; set; } = "/inventory";

	public string NextLabel { get; set; } = "Back to Inventory";
}

