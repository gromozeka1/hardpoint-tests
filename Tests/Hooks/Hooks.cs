using HardPoint.Testing.Driver;
using Reqnroll;
using Tests.Models;
using Tests.Support;

namespace Tests.Hooks;

[Binding]
public class Hooks(ProductsApi productsApi) : BaseHook()
{
    [AfterScenario]
    public async Task DeleteCreatedProduct(ScenarioContext scenarioContext)
    {
        if (scenarioContext.TryGetValue(out Product product))
        {
            await productsApi.DeleteByNameAsync(product.Name);
        }

        if (scenarioContext.TryGetValue("ProductsToDelete", out List<string> toDelete))
        {
            foreach (var name in toDelete)
            {
                await productsApi.DeleteByNameAsync(name);
            }
        }
    }
}
