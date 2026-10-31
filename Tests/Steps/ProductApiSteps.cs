using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;
using Reqnroll;
using Tests.Models;
using Tests.Support;

namespace Tests.Steps;

[Binding]
public sealed class ProductApiSteps(ScenarioContext scenarioContext, ProductNames productNames, ProductsApi productsApi)
{
    private IAPIResponse _response = null!;

    [Given("a product exists with the following details")]
    public async Task GivenAProductExists(DataTable table)
    {
        Product product = table.CreateInstance<Product>();
        product.Name = productNames.Register(product.Name);

        await productsApi.DeleteByNameAsync(product.Name);

        IAPIResponse response = await productsApi.CreateAsync(product);
        _ = response.Status.Should().Be(201);

        Product created = await ProductsApi.ReadAsync(response);
        scenarioContext.Set(created);
        TrackProductForCleanup(created.Name);
    }

    [Given("another product exists with the following details")]
    public async Task GivenAnotherProductExists(DataTable table)
    {
        Product product = table.CreateInstance<Product>();
        product.Name = productNames.Register(product.Name);

        await productsApi.DeleteByNameAsync(product.Name);

        IAPIResponse response = await productsApi.CreateAsync(product);
        _ = response.Status.Should().Be(201);

        Product created = await ProductsApi.ReadAsync(response);
        scenarioContext.Set(created, "SecondProduct");
        TrackProductForCleanup(created.Name);
    }

    [When("I request the product with id {int}")]
    public async Task WhenIRequestTheProductWithId(int id) => _response = await productsApi.GetAsync(id);

    [When("I request the created product")]
    public async Task WhenIRequestTheCreatedProduct() => _response = await productsApi.GetAsync(scenarioContext.Get<Product>().Id);

    [When("I delete the created product")]
    public async Task WhenIDeleteTheCreatedProduct() => _response = await productsApi.DeleteAsync(scenarioContext.Get<Product>().Id);

    [When("I create a product with missing name")]
    public async Task WhenICreateAProductWithMissingName()
    {
        var json = JsonSerializer.Serialize(new
        {
            Description = "Missing name test",
            Price = 10,
            Stock = 1,
            ProductType = ProductType.CPU
        });
        _response = await productsApi.CreateRawAsync(json);
    }

    [When("I create a product with a name longer than 100 characters")]
    public async Task WhenICreateAProductWithANameLongerThan100Characters()
    {
        var longName = new string('A', 101);
        var json = JsonSerializer.Serialize(new
        {
            Name = longName,
            Description = "Long name test",
            Price = 10,
            Stock = 1,
            ProductType = ProductType.CPU
        });
        _response = await productsApi.CreateRawAsync(json);
    }

    [When("I create a product with negative price {int}")]
    public async Task WhenICreateAProductWithNegativePrice(int price)
    {
        var json = JsonSerializer.Serialize(new
        {
            Name = "Negative price product",
            Description = "Negative price test",
            Price = price,
            Stock = 1,
            ProductType = ProductType.CPU
        });
        _response = await productsApi.CreateRawAsync(json);
    }

    [When("I create product with following details via API")]
    public async Task WhenICreateProductWithFollowingDetailsViaApi(DataTable table)
    {
        Product product = table.CreateInstance<Product>();
        product.Name = productNames.Register(product.Name);

        TrackProductForCleanup(product.Name);
        _response = await productsApi.CreateAsync(product);
    }

    [When("I update the second product name to {string}")]
    public async Task WhenIUpdateTheSecondProductNameTo(string newName)
    {
        Product second = scenarioContext.Get<Product>("SecondProduct");
        second.Name = productNames.Actual(newName);
        _response = await productsApi.UpdateAsync(second.Id, second);
    }

    [When("I update the created product with the same name")]
    public async Task WhenIUpdateTheCreatedProductWithTheSameName()
    {
        Product product = scenarioContext.Get<Product>();
        _response = await productsApi.UpdateAsync(product.Id, product);
    }

    [When("I search for products with name {string}")]
    public async Task WhenISearchForProductsWithName(string name)
    {
        _response = await productsApi.GetAllAsync(search: name);
    }

    [When("I search for products with name {string} and type {string}")]
    public async Task WhenISearchForProductsWithNameAndType(string name, string type)
    {
        _response = await productsApi.GetAllAsync(search: productNames.Actual(name), type: type);
    }

    [When("I POST a product named {string} with price {int} and stock {int}")]
    public async Task WhenIPostAProductNamedWithPriceAndStock(string name, int price, int stock)
    {
        var actual = productNames.Register(name);
        TrackProductForCleanup(actual);

        _response = await productsApi.CreateRawAsync(JsonSerializer.Serialize(new
        {
            Name = actual,
            Description = "Range validation",
            Price = price,
            Stock = stock,
            ProductType = ProductType.CPU
        }));
    }

    [When("I PUT the created product with a body id offset by {int}")]
    public async Task WhenIPutTheCreatedProductWithABodyIdOffsetBy(int offset)
    {
        Product product = scenarioContext.Get<Product>();
        _response = await productsApi.UpdateWithBodyIdAsync(product.Id, product.Id + offset, product);
    }

    [When("I filter products by type {string}")]
    public async Task WhenIFilterProductsByType(string type)
    {
        _response = await productsApi.GetAllAsync(type: type);
    }

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status) => _ = _response.Status.Should().Be(status);

    [Then("the product has name {string} and price {int}")]
    public async Task ThenTheProductHasNameAndPrice(string name, int price)
    {
        Product product = await ProductsApi.ReadAsync(_response);
        _ = product.Name.Should().Be(productNames.Actual(name));
        _ = product.Price.Should().Be(price);
    }

    [Then("the product has stock {int}")]
    public async Task ThenTheProductHasStock(int stock)
    {
        Product product = await ProductsApi.ReadAsync(_response);
        _ = product.Stock.Should().Be(stock);
    }

    [Then("the response detail contains {string}")]
    public async Task ThenTheResponseDetailContains(string text)
    {
        JsonElement? json = await _response.JsonAsync();
        string detail = json?.GetProperty("detail").GetString() ?? string.Empty;
        _ = detail.Should().Contain(text);
    }

    [Then("the product list contains {string}")]
    public async Task ThenTheProductListContains(string name)
    {
        List<Product> products = await ProductsApi.ReadListAsync(_response);
        string actual = productNames.Actual(name);
        _ = products.Any(p => p.Name.Contains(actual, StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Then("the product list does not contain {string}")]
    public async Task ThenTheProductListDoesNotContain(string name)
    {
        List<Product> products = await ProductsApi.ReadListAsync(_response);
        string actual = productNames.Actual(name);
        _ = products.Any(p => p.Name.Contains(actual, StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
    }

    [Then("all products in the response have type {string}")]
    public async Task ThenAllProductsInTheResponseHaveType(string type)
    {
        List<Product> products = await ProductsApi.ReadListAsync(_response);
        ProductType expected = Enum.Parse<ProductType>(type, ignoreCase: true);
        _ = products.Should().NotBeEmpty();
        _ = products.All(p => p.ProductType == expected).Should().BeTrue();
    }

    private void TrackProductForCleanup(string name)
    {
        if (!scenarioContext.TryGetValue("ProductsToDelete", out List<string> toDelete))
        {
            toDelete = [];
            scenarioContext.Set(toDelete, "ProductsToDelete");
        }

        if (!toDelete.Contains(name))
        {
            toDelete.Add(name);
        }
    }
}
