using HardPoint.Testing.Driver;
using Microsoft.Playwright;

namespace Tests.Pages;

public class ProductListPage(PlaywrightDriver playwrightDriver) : BasePage(playwrightDriver)
{
    public async Task CreateProductAsync()
    {
        IPage page = await GetPageAsync();
        await page.GetByRole(AriaRole.Navigation)
            .GetByRole(AriaRole.List)
            .GetByRole(AriaRole.Link, new() { Name = "Products" })
            .ClickAsync();
        await ClickLinkAsync("Add product");
    }

    public async Task ClickProductFromListAsync(string name)
        => await (await RowAsync(name)).GetByRole(AriaRole.Link, new() { Name = "Details" }).ClickAsync();

    public async Task SearchAsync(string? searchTerm, string? type = null)
    {
        IPage page = await GetPageAsync();
        if (searchTerm is not null)
        {
            await page.GetByPlaceholder("Search by name").FillAsync(searchTerm);
        }

        if (type is not null)
        {
            _ = await page.GetByRole(AriaRole.Combobox, new() { Name = "Filter by type" }).SelectOptionAsync(type);
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
    }

    public async Task<ILocator> StockBadgeForProductAsync(string productName)
        => (await RowAsync(productName)).Locator(".badge");

    public Task<ILocator> ProductRowAsync(string productName) => RowAsync(productName);

    public Task<ILocator> IsProductCreatedAsync(string product) => TextAsync(product);
}
