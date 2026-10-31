using System.Globalization;
using HardPoint.Testing.Driver;
using Microsoft.Playwright;
using Tests.Models;

namespace Tests.Pages;

public class ProductPage(PlaywrightDriver playwrightDriver) : BasePage(playwrightDriver)
{
    public async Task CreateProductAsync(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        IPage page = await GetPageAsync();
        await page.GetByLabel("Name").FillAsync(product.Name);
        await page.GetByLabel("Description").FillAsync(product.Description);
        await page.Locator("#Price").FillAsync(product.Price.ToString(CultureInfo.InvariantCulture));
        await page.Locator("#Stock").FillAsync(product.Stock.ToString(CultureInfo.InvariantCulture));
        _ = await page.GetByRole(AriaRole.Combobox, new() { Name = "Product type" })
            .SelectOptionAsync(((int)product.ProductType).ToString(CultureInfo.InvariantCulture));
    }

    public async Task ClickCreateAsync() => await ClickButtonAsync("Create product");
}
