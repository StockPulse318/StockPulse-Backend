using System.Net;
using System.Net.Http.Json;
using StockPulse.API.DTOs;
using Xunit;

namespace StockPulse.Tests;

public sealed class RolePermissionTests : IClassFixture<StockPulseTestFixture>
{
    private readonly StockPulseTestFixture _fixture;

    public RolePermissionTests(StockPulseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Unauthenticated_Requests_Return_401()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error.Error.Code);
    }

    [Fact]
    public async Task Manager_Can_Create_Update_And_Delete_Products()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateManagerClientAsync();

        // 1. Create product
        var createRequest = new CreateProductRequest(
            ProductCode: "TEST-MGR-01",
            Name: "Manager Test Product",
            CategoryId: 1,
            Quantity: 50,
            UnitPrice: 120.00m,
            ReorderLevel: 10);

        var createResponse = await client.PostAsJsonAsync("/products", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.Equal("TEST-MGR-01", created.ProductCode);

        // 2. Update product
        var updateRequest = new UpdateProductRequest(
            ProductCode: "TEST-MGR-01",
            Name: "Manager Test Product Updated",
            CategoryId: 1,
            UnitPrice: 140.00m,
            ReorderLevel: 15);

        var updateResponse = await client.PutAsJsonAsync($"/products/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Manager Test Product Updated", updated.Name);

        // 3. Delete product
        var deleteResponse = await client.DeleteAsync($"/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it is gone
        var getResponse = await client.GetAsync($"/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Clerk_Cannot_Add_Modify_Or_Delete_Products()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        // 1. Clerk attempts to add product -> 403 Forbidden
        var createRequest = new CreateProductRequest(
            ProductCode: "CLERK-FORBIDDEN",
            Name: "Clerk Product Attempt",
            CategoryId: 1,
            Quantity: 20,
            UnitPrice: 50.00m,
            ReorderLevel: 5);

        var createResponse = await client.PostAsJsonAsync("/products", createRequest);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        var error = await createResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("FORBIDDEN", error.Error.Code);

        // 2. Clerk attempts to update existing product (PRD-1001) -> 403 Forbidden
        var updateRequest = new UpdateProductRequest(
            ProductCode: "PRD-1001",
            Name: "Hacked Name",
            CategoryId: 1,
            UnitPrice: 1.00m,
            ReorderLevel: 1);

        var updateResponse = await client.PutAsJsonAsync("/products/1", updateRequest);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        // 3. Clerk attempts to delete product -> 403 Forbidden
        var deleteResponse = await client.DeleteAsync("/products/1");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Clerk_Can_View_Search_StockIn_StockOut_And_View_Alerts()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        // 1. View products
        var listResponse = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // 2. Search products
        var searchResponse = await client.GetAsync("/products?q=Cement");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        // 3. Stock-in
        var stockInResponse = await client.PostAsJsonAsync("/products/1/stock-in", new StockMovementRequest(5));
        Assert.Equal(HttpStatusCode.OK, stockInResponse.StatusCode);

        // 4. Stock-out
        var stockOutResponse = await client.PostAsJsonAsync("/products/1/stock-out", new StockMovementRequest(3));
        Assert.Equal(HttpStatusCode.OK, stockOutResponse.StatusCode);

        // 5. View low-stock alerts
        var alertsResponse = await client.GetAsync("/alerts/low-stock");
        Assert.Equal(HttpStatusCode.OK, alertsResponse.StatusCode);
    }
}
