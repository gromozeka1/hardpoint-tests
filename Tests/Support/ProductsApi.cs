using System.Text.Json;
using HardPoint.Testing.Driver;
using Microsoft.Playwright;
using Tests.Models;

namespace Tests.Support;

public class ProductsApi(PlaywrightDriver playwrightDriver)
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Task<IAPIRequestContext> Api => playwrightDriver.ApiRequestContext;

    public async Task<IAPIResponse> GetAsync(int id) => await (await Api).GetAsync($"products/{id}");

    public async Task<IAPIResponse> GetAllAsync(string? search = null, string? type = null)
    {
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            queryParts.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            queryParts.Add($"type={Uri.EscapeDataString(type)}");
        }

        var url = queryParts.Count > 0 ? $"products?{string.Join("&", queryParts)}" : "products";
        return await (await Api).GetAsync(url);
    }

    public async Task<IAPIResponse> DeleteAsync(int id) => await (await Api).DeleteAsync($"products/{id}");

    public async Task<IAPIResponse> CreateAsync(Product product) => await (await Api).PostAsync("products", new()
    {
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Data = JsonSerializer.Serialize(new
        {
            product.Name,
            product.Description,
            product.ProductType,
            product.Price,
            product.Stock
        })
    });

    public async Task<IAPIResponse> CreateRawAsync(string json) => await (await Api).PostAsync("products", new()
    {
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Data = json
    });

    public async Task<IAPIResponse> UpdateAsync(int id, Product product) => await (await Api).PutAsync($"products/{id}", new()
    {
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Data = JsonSerializer.Serialize(new
        {
            Id = id,
            product.Name,
            product.Description,
            product.ProductType,
            product.Price,
            product.Stock
        })
    });

    /// <summary>
    /// PUTs to <paramref name="routeId"/> while the body claims another id, which the API must
    /// reject with 400.
    /// </summary>
    public async Task<IAPIResponse> UpdateWithBodyIdAsync(int routeId, int bodyId, Product product) => await (await Api).PutAsync($"products/{routeId}", new()
    {
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Data = JsonSerializer.Serialize(new
        {
            Id = bodyId,
            product.Name,
            product.Description,
            product.ProductType,
            product.Price,
            product.Stock
        })
    });

    public static async Task<Product> ReadAsync(IAPIResponse response)
    {
        JsonElement? data = await response.JsonAsync();

        return data?.Deserialize<Product>(_jsonSerializerOptions)
            ?? throw new JsonException("The Product API returned an empty product response.");
    }

    public static async Task<List<Product>> ReadListAsync(IAPIResponse response)
    {
        JsonElement? data = await response.JsonAsync();

        return data?.Deserialize<List<Product>>(_jsonSerializerOptions)
            ?? throw new JsonException("The Product API returned an empty product list response.");
    }

    public async Task DeleteByNameAsync(string name)
    {
        IAPIResponse listResponse = await (await Api).GetAsync("products");
        JsonElement? data = await listResponse.JsonAsync();
        IEnumerable<Product> products = data?.Deserialize<List<Product>>(_jsonSerializerOptions) ?? [];

        foreach (Product match in products.Where(p => p.Name == name))
        {
            IAPIResponse deleteResponse = await DeleteAsync(match.Id);
            if (deleteResponse.Status is not (204 or 404))
            {
                throw new InvalidOperationException($"Deleting product '{name}' failed with HTTP {deleteResponse.Status}.");
            }
        }
    }
}
