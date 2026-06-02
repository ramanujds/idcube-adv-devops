using Microsoft.AspNetCore.Mvc;
using PartInventoryService.DotNet.Models;
using PartInventoryService.DotNet.Repositories;

namespace PartInventoryService.DotNet.Controllers;

[ApiController]
[Route("api/parts")]
public class PartsApiController : ControllerBase
{
    private readonly IPartRepository _partRepository;
    private readonly ILogger<PartsApiController> _logger;

    public PartsApiController(IPartRepository partRepository, ILogger<PartsApiController> logger)
    {
        _partRepository = partRepository;
        _logger = logger;
    }

    [HttpPost]
    public ActionResult<Part> Create([FromBody] PartInputModel input)
    {
        _logger.LogInformation("Creating new part with SKU: {Sku}", input.Sku);
        var savedPart = _partRepository.Create(new Part
        {
            Id = Guid.NewGuid().ToString(),
            Sku = input.Sku,
            Name = input.Name,
            Price = input.Price,
            Stock = input.Stock
        });
        _logger.LogInformation("Part created successfully with ID: {Id}, SKU: {Sku}", savedPart.Id, savedPart.Sku);
        return Ok(savedPart);
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Part>> GetAll()
    {
        _logger.LogInformation("Fetching all parts");
        var parts = _partRepository.FindAll();
        _logger.LogInformation("Retrieved {Count} parts", parts.Count);
        return Ok(parts);
    }

    [HttpGet("{id}")]
    public ActionResult<Part> GetById(string id)
    {
        _logger.LogInformation("Fetching part with ID: {Id}", id);
        var part = _partRepository.FindById(id);
        if (part is null)
        {
            _logger.LogWarning("Part not found with ID: {Id}", id);
            return NotFound();
        }
        return Ok(part);
    }

    [HttpGet("sku/{sku}")]
    public ActionResult<IReadOnlyList<Part>> GetBySku(string sku)
    {
        _logger.LogInformation("Fetching parts with SKU: {Sku}", sku);
        var parts = _partRepository.FindBySku(sku);
        _logger.LogInformation("Retrieved {Count} parts for SKU: {Sku}", parts.Count, sku);
        return Ok(parts);
    }

    [HttpPut("{id}")]
    public ActionResult<Part> Update(string id, [FromBody] PartInputModel input)
    {
        _logger.LogInformation("Updating part with ID: {Id}", id);
        if (!_partRepository.ExistsById(id))
        {
            _logger.LogWarning("Update failed - part not found with ID: {Id}", id);
            return NotFound();
        }
        var updated = _partRepository.Update(id, input);
        _logger.LogInformation("Part updated successfully with ID: {Id}", id);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        _logger.LogInformation("Deleting part with ID: {Id}", id);
        if (!_partRepository.DeleteById(id))
        {
            _logger.LogWarning("Delete failed - part not found with ID: {Id}", id);
            return NotFound();
        }
        _logger.LogInformation("Part deleted successfully with ID: {Id}", id);
        return NoContent();
    }

    [HttpPost("place-order")]
    public ActionResult<OrderResponse> PlaceOrder([FromBody] OrderRequest request)
    {
        _logger.LogInformation("Placing order for SKU: {Sku}, quantity: {Quantity}", request.Sku, request.Quantity);
        var parts = _partRepository.FindBySku(request.Sku);
        if (parts.Count == 0)
        {
            _logger.LogWarning("Order failed - part not found for SKU: {Sku}", request.Sku);
            return BadRequest(new OrderResponse
            {
                PartSku = string.Empty,
                Status = "Part not found",
                Quantity = 0,
                TotalPrice = 0
            });
        }

        var part = parts[0];
        if (part.Stock < request.Quantity)
        {
            _logger.LogWarning("Order failed - insufficient stock for SKU: {Sku}. Requested: {Requested}, Available: {Available}",
                request.Sku, request.Quantity, part.Stock);
            return BadRequest(new OrderResponse
            {
                PartSku = string.Empty,
                Status = "Insufficient stock",
                Quantity = 0,
                TotalPrice = 0
            });
        }

        _partRepository.DecrementStock(part.Id, request.Quantity);
        _logger.LogInformation("Order placed successfully for SKU: {Sku}, quantity: {Quantity}, total: {Total}",
            part.Sku, request.Quantity, part.Price * request.Quantity);

        return Ok(new OrderResponse
        {
            PartSku = part.Sku,
            Status = "Order placed successfully",
            Quantity = request.Quantity,
            TotalPrice = part.Price * request.Quantity
        });
    }
}

