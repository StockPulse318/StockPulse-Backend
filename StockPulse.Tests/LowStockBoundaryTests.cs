using System.Net;
using System.Net.Http.Json;
using StockPulse.API.DTOs;
using Xunit;

namespace StockPulse.Tests;

public sealed class LowStockBoundaryTests : IClassFixture<StockPulseTestFixture>
{
    private readonly StockPulseTestFixture _fixture;

    public LowStockBoundaryTests(StockPulseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LowStock_Evaluates_Exactly_At_Boundary_Condition_Quantity_Equals_ReorderLevel()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateManagerClientAsync();

        // 1. Create product at boundary: Quantity = 15, ReorderLevel = 15
        var boundaryRequest = new CreateProductRequest(
            ProductCode: $"BOUND-EQ-{Guid.NewGuid():N}"[..14],
            Name: "Boundary Equal Product",
            CategoryId: 1,
            Quantity: 15,
            UnitPrice: 100m,
            ReorderLevel: 15);

        var boundaryResponse = await client.PostAsJsonAsync("/products", boundaryRequest);
        boundaryResponse.EnsureSuccessStatusCode();
        var boundaryProduct = await boundaryResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(boundaryProduct);

        // When Quantity == ReorderLevel, is_low_stock MUST be true
        Assert.True(boundaryProduct.IsLowStock);

        // 2. Create product just above boundary: Quantity = 16, ReorderLevel = 15
        var aboveRequest = new CreateProductRequest(
            ProductCode: $"BOUND-GT-{Guid.NewGuid():N}"[..14],
            Name: "Boundary Greater Product",
            CategoryId: 1,
            Quantity: 16,
            UnitPrice: 100m,
            ReorderLevel: 15);

        var aboveResponse = await client.PostAsJsonAsync("/products", aboveRequest);
        aboveResponse.EnsureSuccessStatusCode();
        var aboveProduct = await aboveResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(aboveProduct);

        // When Quantity > ReorderLevel, is_low_stock MUST be false
        Assert.False(aboveProduct.IsLowStock);

        // 3. Verify GET /alerts/low-stock
        var alertsResponse = await client.GetAsync("/alerts/low-stock");
        Assert.Equal(HttpStatusCode.OK, alertsResponse.StatusCode);

        var alertProducts = await alertsResponse.Content.ReadFromJsonAsync<IEnumerable<ProductResponse>>();
        Assert.NotNull(alertProducts);

        var alertList = alertProducts.ToList();

        // The product where quantity == reorder_level MUST be in alerts
        Assert.Contains(alertList, p => p.Id == boundaryProduct.Id);

        // The product where quantity > reorder_level MUST NOT be in alerts
        Assert.DoesNotContain(alertList, p => p.Id == aboveProduct.Id);

        // Every item returned by /alerts/low-stock must strictly satisfy quantity <= reorder_level
        Assert.All(alertList, p =>
        {
            Assert.True(p.Quantity <= p.ReorderLevel);
            Assert.True(p.IsLowStock);
        });
    }
}
