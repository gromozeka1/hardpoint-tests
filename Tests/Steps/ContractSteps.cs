using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Json.Schema;
using Microsoft.Playwright;
using Reqnroll;
using Tests.Models;
using Tests.Support;

namespace Tests.Steps;

/// <summary>
/// Checks the suite's assumptions against the OpenAPI document the ProductAPI publishes: the
/// endpoints it calls, the fields its models mirror, and the shape of the responses it reads.
/// These fail when the API changes shape, before a UI or API scenario has to stumble over it.
/// </summary>
[Binding]
public sealed class ContractSteps(ApiContract apiContract, ProductsApi productsApi, ScenarioContext scenarioContext)
{
    private const string _productSchema = "Product";

    [Given("the ProductAPI publishes an OpenAPI document")]
    public async Task GivenTheProductApiPublishesAnOpenApiDocument()
    {
        JsonObject document = await apiContract.GetDocumentAsync();

        _ = document["openapi"]?.GetValue<string>().Should().StartWith("3.",
            "the service should describe itself as an OpenAPI 3.x document");
        _ = (document["paths"] as JsonObject).Should().NotBeNull("the document should declare the endpoints");
    }

    [Then("it describes the Product schema that the test model mirrors")]
    public async Task ThenItDescribesTheProductSchemaThatTheTestModelMirrors()
    {
        JsonObject schema = await apiContract.GetDeclaredSchemaAsync(_productSchema);

        JsonObject properties = schema["properties"] as JsonObject
            ?? throw new InvalidOperationException("The published Product schema declares no properties.");

        string[] expected = [.. typeof(Product).GetProperties()
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
            .Order(StringComparer.Ordinal)];

        _ = properties.Select(entry => entry.Key).Order(StringComparer.Ordinal).Should().Equal(expected,
            "the published contract and Tests/Models/Product.cs must describe the same fields");

        JsonArray required = schema["required"] as JsonArray
            ?? throw new InvalidOperationException("The published Product schema declares no required fields.");

        _ = required.Select(node => node!.GetValue<string>()).Should().Contain("name",
            "Name is the only field the API requires");
    }

    [Then("it describes ProductType as a number")]
    public async Task ThenItDescribesProductTypeAsANumber()
    {
        JsonObject schema = await apiContract.GetDeclaredSchemaAsync("ProductType");
        JsonNode? declaredType = schema["type"]
            ?? throw new InvalidOperationException("The published ProductType schema declares no type.");

        string[] types = declaredType is JsonArray typeArray
            ? [.. typeArray.Select(node => node!.GetValue<string>())]
            : [declaredType.GetValue<string>()];

        _ = types.Should().Contain("integer",
            "enum values are serialised as numbers, which the WebApp and Tests/Models both depend on");
    }

    [Then("it exposes {string} on {string}")]
    public async Task ThenItExposesOn(string method, string path)
    {
        JsonObject document = await apiContract.GetDocumentAsync();
        JsonObject paths = document["paths"] as JsonObject
            ?? throw new InvalidOperationException("The OpenAPI document declares no paths.");

        JsonObject operations = paths[path] as JsonObject
            ?? throw new InvalidOperationException(
                $"The OpenAPI document declares no path '{path}'. Declared paths: {string.Join(", ", paths.Select(entry => entry.Key))}.");

        _ = operations.ContainsKey(method).Should().BeTrue(
            $"the document should declare {method.ToUpperInvariant()} {path}");
    }

    [Then("a payload that omits the required name does not conform to the Product schema")]
    public async Task ThenAPayloadThatOmitsTheRequiredNameDoesNotConform()
    {
        JsonSchema schema = await apiContract.GetCompiledSchemaAsync(_productSchema);
        var payload = JsonNode.Parse("""{"id":1,"price":10,"stock":1,"productType":0}""")!;

        string? mismatch = await apiContract.FindMismatchAsync(payload, schema);

        _ = mismatch.Should().NotBeNull(
            "otherwise the schema checks above would accept any payload and prove nothing");
    }

    [Then("the product list response matches the Product schema")]
    public async Task ThenTheProductListResponseMatchesTheProductSchema()
    {
        IAPIResponse response = await productsApi.GetAllAsync();
        _ = response.Status.Should().Be(200);

        JsonSchema schema = await apiContract.GetArraySchemaAsync(_productSchema);
        string? mismatch = await apiContract.FindMismatchAsync(await ReadBodyAsync(response), schema);

        _ = mismatch.Should().BeNull("every product in the list should conform to the published schema");
    }

    [Then("the created product response matches the Product schema")]
    public async Task ThenTheCreatedProductResponseMatchesTheProductSchema()
    {
        Product product = scenarioContext.Get<Product>();

        IAPIResponse response = await productsApi.GetAsync(product.Id);
        _ = response.Status.Should().Be(200);

        JsonSchema schema = await apiContract.GetCompiledSchemaAsync(_productSchema);
        string? mismatch = await apiContract.FindMismatchAsync(await ReadBodyAsync(response), schema);

        _ = mismatch.Should().BeNull("the product response should conform to the published schema");
    }

    private static async Task<JsonNode> ReadBodyAsync(IAPIResponse response)
        => JsonNode.Parse(await response.TextAsync())
            ?? throw new InvalidOperationException("The Product API returned an empty response body.");
}
