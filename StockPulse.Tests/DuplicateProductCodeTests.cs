using System.Net;
using System.Net.Http.Json;
using StockPulse.API.DTOs;
using Xunit;

namespace StockPulse.Tests;

public sealed class DuplicateProductCodeTests : IClassFixture<StockPulseTestFixture>
{
    private readonly StockPulseTestFixture _fixture;

    public DuplicateProductCodeTests(StockPulseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_Product_With_Existing_ProductCode_Returns_409_Conflict()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateManagerClientAsync();

        // PRD-1001 exists from seeding
        var duplicateRequest = new CreateProductRequest(
            ProductCode: "PRD-1001",
            Name: "Duplicate Code Attempt",
            CategoryId: 1,
            Quantity: 20,
            UnitPrice: 100.00m,
            ReorderLevel: 5);

        var response = await client.PostAsJsonAsync("/products", duplicateRequest);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("DUPLICATE_PRODUCT_CODE", error.Error.Code);
        Assert.Contains("PRD-1001", error.Error.Message);
    }

    [Fact]
    public async Task Update_Product_With_Conflicting_ProductCode_Returns_409_Conflict()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateManagerClientAsync();

        // Attempt to update PRD-1002 to use PRD-1001's code
        var updateRequest = new UpdateProductRequest(
            ProductCode: "PRD-1001",
            Name: "Renamed Product",
            CategoryId: 1,
            UnitPrice: 150.00m,
            ReorderLevel: 10);

        var response = await client.PutAsJsonAsync("/products/2", updateRequest);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("DUPLICATE_PRODUCT_CODE", error.Error.Code);
    }
}
