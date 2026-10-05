using System.Net;
using System.Net.Http.Json;
using StockPulse.API.DTOs;
using Xunit;

namespace StockPulse.Tests;

public sealed class ProductSearchTests : IClassFixture<StockPulseTestFixture>
{
    private readonly StockPulseTestFixture _fixture;

    public ProductSearchTests(StockPulseTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Search_By_Product_Name_Case_Insensitive_Partial_Match()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        var response = await client.GetAsync("/products?q=cement");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.True(page.Total > 0);
        Assert.All(page.Items, item => Assert.Contains("cement", item.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_By_Product_Code_Case_Insensitive_Partial_Match()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        // Search by exact code or partial code
        var response = await client.GetAsync("/products?q=prd-1008");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.True(page.Total >= 1);
        Assert.Contains(page.Items, item => item.ProductCode == "PRD-1008");
    }

    [Fact]
    public async Task Pagination_And_Sorting_Work_Correctly()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        // Test page size limit
        var pageResponse = await client.GetAsync("/products?page=1&limit=5");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);

        var page = await pageResponse.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.Equal(5, page.Items.Count());
        Assert.Equal(1, page.Page);
        Assert.Equal(5, page.Limit);
        Assert.True(page.Total >= 50);

        // Test sorting by quantity ascending
        var sortAscResponse = await client.GetAsync("/products?sortBy=quantity&sortOrder=asc&limit=10");
        var sortAscPage = await sortAscResponse.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(sortAscPage);

        var quantities = sortAscPage.Items.Select(p => p.Quantity).ToList();
        for (int i = 0; i < quantities.Count - 1; i++)
        {
            Assert.True(quantities[i] <= quantities[i + 1]);
        }

        // Test sorting by quantity descending
        var sortDescResponse = await client.GetAsync("/products?sortBy=quantity&sortOrder=desc&limit=10");
        var sortDescPage = await sortDescResponse.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(sortDescPage);

        var descQuantities = sortDescPage.Items.Select(p => p.Quantity).ToList();
        for (int i = 0; i < descQuantities.Count - 1; i++)
        {
            Assert.True(descQuantities[i] >= descQuantities[i + 1]);
        }
    }

    [Fact]
    public async Task Category_Filter_Returns_Only_Matching_Category()
    {
        await _fixture.InitializeDatabaseAsync();
        var client = await _fixture.CreateClerkClientAsync();

        var response = await client.GetAsync("/products?category_id=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.True(page.Total > 0);
        Assert.All(page.Items, item => Assert.Equal(1, item.CategoryId));
    }
}
