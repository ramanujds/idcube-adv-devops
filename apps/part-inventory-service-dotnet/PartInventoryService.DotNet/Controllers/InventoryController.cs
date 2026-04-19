using Microsoft.AspNetCore.Mvc;
using PartInventoryService.DotNet.Models;
using PartInventoryService.DotNet.Repositories;

namespace PartInventoryService.DotNet.Controllers;

public class InventoryController : Controller
{
    private readonly IPartRepository _partRepository;

    public InventoryController(IPartRepository partRepository)
    {
        _partRepository = partRepository;
    }

    [HttpGet("")]
    [HttpGet("inventory")]
    public IActionResult Index()
    {
        return View("Index", _partRepository.FindAll());
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
        _partRepository.Create(new Part
        {
            Id = Guid.NewGuid().ToString(),
            Sku = input.Sku,
            Name = input.Name,
            Price = input.Price,
            Stock = input.Stock
        });

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
        var part = _partRepository.FindById(id);
        if (part is null)
        {
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
        _partRepository.Update(id, input);

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
        _partRepository.DeleteById(id);

        return View("InventoryUpdate", new InventoryUpdateViewModel
        {
            Type = "success",
            Message = "Part deleted successfully.",
            NextUrl = "/inventory",
            NextLabel = "Back to Inventory"
        });
    }
}

