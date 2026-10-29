using HardPoint.Testing.Driver;
using Microsoft.Playwright;

namespace Tests.Pages;

public class ProductListPage(PlaywrightDriver playwrightDriver) : BasePage(playwrightDriver)
{
    private ILocator LnkProductList => Page.GetByRole(AriaRole.Navigation)
        .GetByRole(AriaRole.List)
        .GetByRole(AriaRole.Link, new() { Name = "Products" });

    private ILocator TxtSearch => Page.GetByPlaceholder("Search by name");

    private ILocator SelectTypeFilter => Page.GetByRole(AriaRole.Combobox, new() { Name = "Filter by type" });

    private ILocator BtnSearch => Page.GetByRole(AriaRole.Button, new() { Name = "Search" });

    public async Task CreateProductAsync()
    {
        await LnkProductList.ClickAsync();
        await ClickLinkAsync("Add product");
    }

    public async Task ClickProductFromList(string name)
        => await Row(name).GetByRole(AriaRole.Link, new() { Name = "Details" }).ClickAsync();

    public async Task SearchAsync(string? searchTerm, string? type = null)
    {
        if (searchTerm is not null)
        {
            await TxtSearch.FillAsync(searchTerm);
        }

        if (type is not null)
        {
            _ = await SelectTypeFilter.SelectOptionAsync(type);
        }

        await BtnSearch.ClickAsync();
    }

    public ILocator StockBadgeForProduct(string productName)
        => Row(productName).Locator(".badge");

    public ILocator ProductRow(string productName) => Row(productName);

    public ILocator IsProductCreated(string product) => Text(product);
}
