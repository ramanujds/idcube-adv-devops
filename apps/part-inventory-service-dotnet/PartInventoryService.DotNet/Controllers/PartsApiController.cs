using Microsoft.AspNetCore.Mvc;
using PartInventoryService.DotNet.Models;
using PartInventoryService.DotNet.Repositories;

namespace PartInventoryService.DotNet.Controllers;

[ApiController]
[Route("api/parts")]
public class PartsApiController : ControllerBase
{
    private readonly IPartRepository _partRepository;

    public PartsApiController(IPartRepository partRepository)
    {
        _partRepository = partRepository;
    }

    [HttpPost]
    public ActionResult<Part> Create([FromBody] PartInputModel input)
    {
        var savedPart = _partRepository.Create(new Part
        {
            Id = Guid.NewGuid().ToString(),
            Sku = input.Sku,
            Name = input.Name,
            Price = input.Price,
            Stock = input.Stock
        });

        return Ok(savedPart);
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Part>> GetAll()
    {
        return Ok(_partRepository.FindAll());
    }

    [HttpGet("{id}")]
    public ActionResult<Part> GetById(string id)
    {
        var part = _partRepository.FindById(id);
        return part is null ? NotFound() : Ok(part);
    }

    [HttpGet("sku/{sku}")]
    public ActionResult<IReadOnlyList<Part>> GetBySku(string sku)
    {
        return Ok(_partRepository.FindBySku(sku));
    }

    [HttpPut("{id}")]
    public ActionResult<Part> Update(string id, [FromBody] PartInputModel input)
    {
        if (!_partRepository.ExistsById(id))
        {
            return NotFound();
        }

        return Ok(_partRepository.Update(id, input));
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        return _partRepository.DeleteById(id) ? NoContent() : NotFound();
    }

    [HttpPost("place-order")]
    public ActionResult<OrderResponse> PlaceOrder([FromBody] OrderRequest request)
    {
        var parts = _partRepository.FindBySku(request.Sku);
        if (parts.Count == 0)
        {
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
            return BadRequest(new OrderResponse
            {
                PartSku = string.Empty,
                Status = "Insufficient stock",
                Quantity = 0,
                TotalPrice = 0
            });
        }

        _partRepository.DecrementStock(part.Id, request.Quantity);

        return Ok(new OrderResponse
        {
            PartSku = part.Sku,
            Status = "Order placed successfully",
            Quantity = request.Quantity,
            TotalPrice = part.Price * request.Quantity
        });
    }
}

