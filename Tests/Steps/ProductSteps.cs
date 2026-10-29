using Microsoft.Playwright;
using Reqnroll;
using Tests.Models;
using Tests.Pages;
using Tests.Support;

namespace Tests.Steps;

[Binding]
public sealed class ProductSteps(ScenarioContext scenarioContext, ProductNames productNames, ProductPage productPage, ProductListPage productListPage)
{
    [Given("I click the Product menu")]
    public async Task GivenIClickTheProductMenu() => await productListPage.CreateProductAsync();

    [StepDefinition("I create product with following details")]
    public async Task GivenICreateProductWithFollowingDetails(DataTable table)
    {
        Product product = table.CreateInstance<Product>();
        product.Name = productNames.Register(product.Name);

        scenarioContext.Set(product);
        TrackProductForCleanup(product.Name);

        await productPage.CreateProduct(product);
        await productPage.ClickCreate();
    }

    [When("I click the Create button without entering details")]
    public async Task WhenIClickTheCreateButtonWithoutEnteringDetails()
    {
        await productPage.ClickCreate();
    }

    [When("I click the Details link of the newly created product")]
    public async Task WhenIClickTheDetailsLinkOfTheNewlyCreatedProduct()
    {
        Product product = scenarioContext.Get<Product>();
        await productListPage.ClickProductFromList(product.Name);
    }

    [When("I search for products by name {string} in UI")]
    public async Task WhenISearchForProductsByNameInUi(string name)
    {
        await productListPage.SearchAsync(productNames.Actual(name));
    }

    [When("I filter products by type {string} in UI")]
    public async Task WhenIFilterProductsByTypeInUi(string type)
    {
        await productListPage.SearchAsync(null, type);
    }

    [Then("I see all the product details are created as expected")]
    public async Task ThenISeeAllTheProductDetailsAreCreatedAsExpected()
    {
        Product product = scenarioContext.Get<Product>();
        await Assertions.Expect(productListPage.IsProductCreated(product.Name)).ToBeVisibleAsync();
    }

    [Then("I see the stock badge {string} for product {string}")]
    public async Task ThenISeeTheStockBadgeForProduct(string badgeText, string productName)
    {
        await Assertions.Expect(productListPage.StockBadgeForProduct(productNames.Actual(productName))).ToHaveTextAsync(badgeText);
    }

    [Then("I see a validation message {string}")]
    public async Task ThenISeeAValidationMessage(string message)
    {
        await Assertions.Expect(productPage.Text(productNames.ActualIn(message))).ToBeVisibleAsync();
    }

    [Then("I see an error message {string}")]
    public async Task ThenISeeAnErrorMessage(string message)
    {
        await Assertions.Expect(productPage.Text(productNames.ActualIn(message))).ToBeVisibleAsync();
    }

    [Then("I see product {string} in the list")]
    public async Task ThenISeeProductInTheList(string name)
    {
        await Assertions.Expect(productListPage.ProductRow(productNames.Actual(name))).ToBeVisibleAsync();
    }

    [Then("I do not see product {string} in the list")]
    public async Task ThenIDoNotSeeProductInTheList(string name)
    {
        await Assertions.Expect(productListPage.ProductRow(productNames.Actual(name))).Not.ToBeVisibleAsync();
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
