using System.Net;
using System.Net.Http.Json;
using StockPulse.API.DTOs;
using Xunit;

namespace StockPulse.Tests;

public sealed class NegativeStockPreventionTests : IClassFixture<StockPulseTestFixture>
{
    private readonly StockPulseTestFixture _fixture;

    public NegativeStockPreventionTests(StockPulseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StockOut_Exceeding_Quantity_Returns_409_Conflict_And_Quantity_Unchanged()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        // PRD-1008 has initial Quantity = 8
        var productBefore = await client.GetFromJsonAsync<ProductResponse>("/products/8");
        Assert.NotNull(productBefore);
        var currentQty = productBefore.Quantity;

        // Attempt to remove more than current quantity
        var response = await client.PostAsJsonAsync($"/products/{productBefore.Id}/stock-out", new StockMovementRequest(currentQty + 10));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("INSUFFICIENT_STOCK", error.Error.Code);

        // Verify quantity is completely unchanged
        var productAfter = await client.GetFromJsonAsync<ProductResponse>($"/products/{productBefore.Id}");
        Assert.NotNull(productAfter);
        Assert.Equal(currentQty, productAfter.Quantity);
        Assert.True(productAfter.Quantity >= 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task StockOut_NonPositive_Amount_Returns_400_Validation_Error(int invalidAmount)
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        var response = await client.PostAsJsonAsync("/products/1/stock-out", new StockMovementRequest(invalidAmount));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDATION_ERROR", error.Error.Code);
    }
}
