using Reqnroll;
using Tests.Support;

namespace Tests.Steps;

[Binding]
public sealed class ReusableSteps(ProductNames productNames, ProductsApi productsApi)
{
    [Given("I ensure {string} data is cleaned up if already exists")]
    public async Task GivenIEnsureDataIsCleanedUpIfAlreadyExists(string productName)
        => await productsApi.DeleteByNameAsync(productNames.Register(productName));
}
