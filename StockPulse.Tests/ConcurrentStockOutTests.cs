using System.Net;
using System.Net.Http.Json;
using StockPulse.API.DTOs;
using Xunit;

namespace StockPulse.Tests;

public sealed class ConcurrentStockOutTests : IClassFixture<StockPulseTestFixture>
{
    private readonly StockPulseTestFixture _fixture;

    public ConcurrentStockOutTests(StockPulseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Concurrent_StockOut_Requests_Prevent_Negative_Stock_And_Maintain_Integrity()
    {
        await _fixture.InitializeDatabaseAsync();
        var managerClient = await _fixture.CreateManagerClientAsync();

        // 1. Create a product with exactly 10 units in stock
        var createRequest = new CreateProductRequest(
            ProductCode: $"CONC-{Guid.NewGuid():N}"[..12],
            Name: "Concurrency Test Product",
            CategoryId: 1,
            Quantity: 10,
            UnitPrice: 50.00m,
            ReorderLevel: 2);

        var createResponse = await managerClient.PostAsJsonAsync("/products", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var product = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);

        // 2. Launch 10 concurrent stock-out requests of 2 units each (total attempted: 20 units)
        const int concurrentRequests = 10;
        const int amountPerRequest = 2;

        var clerkClient = await _fixture.CreateClerkClientAsync();

        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ =>
            clerkClient.PostAsJsonAsync($"/products/{product.Id}/stock-out", new StockMovementRequest(amountPerRequest))
        );

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Since initial quantity was 10 and each request takes 2, exactly 5 must succeed and 5 must be rejected
        Assert.Equal(5, successCount);
        Assert.Equal(5, conflictCount);

        // 3. Verify final stock quantity is exactly 0 and never went negative
        var finalProduct = await managerClient.GetFromJsonAsync<ProductResponse>($"/products/{product.Id}");
        Assert.NotNull(finalProduct);
        Assert.Equal(0, finalProduct.Quantity);
    }
}
