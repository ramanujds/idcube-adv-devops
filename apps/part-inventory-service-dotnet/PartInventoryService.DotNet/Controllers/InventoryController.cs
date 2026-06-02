using Microsoft.AspNetCore.Mvc;
using PartInventoryService.DotNet.Models;
using PartInventoryService.DotNet.Repositories;

namespace PartInventoryService.DotNet.Controllers;

public class InventoryController : Controller
{
    private readonly IPartRepository _partRepository;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IPartRepository partRepository, ILogger<InventoryController> logger)
    {
        _partRepository = partRepository;
        _logger = logger;
    }

    [HttpGet("")]
    [HttpGet("inventory")]
    public IActionResult Index()
    {
        _logger.LogInformation("Loading inventory view");
        var parts = _partRepository.FindAll();
        _logger.LogInformation("Displaying {Count} parts in inventory view", parts.Count);
        return View("Index", parts);
    }

    [HttpGet("inventory-update")]
    public IActionResult InventoryUpdate(string? type, string? message, string? nextUrl, string? nextLabel)
    {
        return View("InventoryUpdate", new InventoryUpdateViewModel
        {
            Type = string.IsNullOrWhiteSpace(type) ? "success" : type,
            Message = string.IsNullOrWhiteSpace(message) ? "Operation completed." : message,
            NextUrl = string.IsNullOrWhiteSpace(nextUrl) ? "/inventory" : nextUrl,
            NextLabel = string.IsNullOrWhiteSpace(nextLabel) ? "Back to Inventory" : nextLabel
        });
    }

    [HttpPost("parts")]
    public IActionResult Create([FromForm] PartInputModel input)
    {
        _logger.LogInformation("Creating part via UI with SKU: {Sku}", input.Sku);
        _partRepository.Create(new Part
        {
            Id = Guid.NewGuid().ToString(),
            Sku = input.Sku,
            Name = input.Name,
            Price = input.Price,
            Stock = input.Stock
        });
        _logger.LogInformation("Part created via UI with SKU: {Sku}", input.Sku);

        return View("InventoryUpdate", new InventoryUpdateViewModel
        {
            Type = "success",
            Message = "Part added successfully.",
            NextUrl = "/inventory",
            NextLabel = "Back to Inventory"
        });
    }

    [HttpGet("parts/{id}/edit")]
    public IActionResult Edit(string id)
    {
        _logger.LogInformation("Loading edit view for part ID: {Id}", id);
        var part = _partRepository.FindById(id);
        if (part is null)
        {
            _logger.LogWarning("Edit view requested for non-existent part ID: {Id}", id);
            return View("InventoryUpdate", new InventoryUpdateViewModel
            {
                Type = "error",
                Message = "Part not found.",
                NextUrl = "/inventory",
                NextLabel = "Back to Inventory"
            });
        }

        return View("EditPart", part);
    }

    [HttpPost("parts/{id}")]
    public IActionResult Update(string id, [FromForm] PartInputModel input)
    {
        _logger.LogInformation("Updating part via UI with ID: {Id}", id);
        _partRepository.Update(id, input);
        _logger.LogInformation("Part updated via UI with ID: {Id}", id);

        return View("InventoryUpdate", new InventoryUpdateViewModel
        {
            Type = "success",
            Message = "Part updated successfully.",
            NextUrl = "/inventory",
            NextLabel = "Back to Inventory"
        });
    }

    [HttpPost("parts/{id}/delete")]
    public IActionResult Delete(string id)
    {
        _logger.LogInformation("Deleting part via UI with ID: {Id}", id);
        _partRepository.DeleteById(id);
        _logger.LogInformation("Part deleted via UI with ID: {Id}", id);

        return View("InventoryUpdate", new InventoryUpdateViewModel
        {
            Type = "success",
            Message = "Part deleted successfully.",
            NextUrl = "/inventory",
            NextLabel = "Back to Inventory"
        });
    }
}

