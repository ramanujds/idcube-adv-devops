using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PartInventoryService.DotNet.Models;

namespace PartInventoryService.DotNet.Tests;

public class InventoryServiceTests
{
  [Fact]
  public async Task LivenessProbeReturnsOk()
  {
    await using var factory = CreateFactory();
    using var client = factory.CreateClient();

    var response = await client.GetAsync("/health/live");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task ReadinessProbeReturnsOk()
  {
    await using var factory = CreateFactory();
    using var client = factory.CreateClient();

    var response = await client.GetAsync("/health/ready");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

    [Fact]
    public async Task GetPartsReturnsSeededData()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/parts");
        var parts = await response.Content.ReadFromJsonAsync<List<Part>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(parts);
        Assert.Equal(10, parts.Count);
        Assert.Equal("SEAT-SAFE45-001", parts[0].Sku);
    }

    [Fact]
    public async Task PostPartsCreatesPartWithGuid()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var payload = new PartInputModel { Sku = "ABC-123", Name = "Demo", Price = 12.5m, Stock = 8 };
        var response = await client.PostAsJsonAsync("/api/parts", payload);
        var savedPart = await response.Content.ReadFromJsonAsync<Part>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(savedPart);
        Assert.True(Guid.TryParse(savedPart.Id, out _));
        Assert.Equal(payload.Sku, savedPart.Sku);
    }

    [Fact]
    public async Task PlaceOrderDecreasesStock()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/parts/place-order", new OrderRequest { Sku = "SEAT-SAFE45-001", Quantity = 2 });
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(orderResponse);
        Assert.Equal("Order placed successfully", orderResponse.Status);
        Assert.Equal(2, orderResponse.Quantity);
        Assert.Equal(59.98m, orderResponse.TotalPrice);

        var partAfter = await client.GetFromJsonAsync<List<Part>>("/api/parts/sku/SEAT-SAFE45-001");
        Assert.NotNull(partAfter);
        Assert.Equal(98, partAfter[0].Stock);
    }

    [Fact]
    public async Task PlaceOrderReturnsBadRequestForUnknownSku()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/parts/place-order", new OrderRequest { Sku = "DOES-NOT-EXIST", Quantity = 2 });
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(orderResponse);
        Assert.Equal("Part not found", orderResponse.Status);
    }

    [Fact]
    public async Task DeleteUnknownPartReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/parts/missing-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InventoryPageRendersSeededPart()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/inventory");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Parts Inventory Management", html);
        Assert.Contains("Seat Safe 45 - Model A", html);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>();
    }
}
