@ui @local
Feature: Product

Background:
  Given I ensure "Headphones" data is cleaned up if already exists

Scenario: Create product and verify the details
  Given I click the Product menu
  And I create product with following details
    | Name       | Description        | Price | ProductType | Stock |
    | Headphones | Noise cancellation |   300 | PERIPHARALS |    10 |
  When I click the Details link of the newly created product
  Then I see all the product details are created as expected

Scenario: Create product with Stock >= 10 shows quantity badge
  Given I ensure "StockHigh" data is cleaned up if already exists
  And I click the Product menu
  When I create product with following details
    | Name      | Description | Price | ProductType | Stock |
    | StockHigh | Stock >= 10 |   100 | CPU         |    15 |
  Then I see the stock badge "15 in stock" for product "StockHigh"

Scenario: Create product with Stock = 0 shows Out of stock badge
  Given I ensure "StockZero" data is cleaned up if already exists
  And I click the Product menu
  When I create product with following details
    | Name      | Description | Price | ProductType | Stock |
    | StockZero | Stock is 0  |    50 | CPU         |     0 |
  Then I see the stock badge "Out of stock" for product "StockZero"

Scenario: Create product with Stock < 10 shows Low stock badge
  Given I ensure "StockLow" data is cleaned up if already exists
  And I click the Product menu
  When I create product with following details
    | Name     | Description | Price | ProductType | Stock |
    | StockLow | Stock is 3  |    75 | CPU         |     3 |
  Then I see the stock badge "Low stock" for product "StockLow"

Scenario: Submit Create form with empty Name triggers client-side validation
  Given I click the Product menu
  When I click the Create button without entering details
  Then I see a validation message "The Name field is required."

Scenario: Create product with duplicate name shows server-side error
  Given I ensure "DuplicateUI" data is cleaned up if already exists
  And a product exists with the following details
    | Name        | Description      | Price | ProductType | Stock |
    | DuplicateUI | Existing product |   100 | CPU         |     5 |
  And I click the Product menu
  When I create product with following details
    | Name        | Description     | Price | ProductType | Stock |
    | DuplicateUI | Same name again |   120 | CPU         |     2 |
  Then I see an error message "A product with the name 'DuplicateUI' already exists."

Scenario: Search by name filters the product list
  Given I ensure "SearchItemAlpha" data is cleaned up if already exists
  And a product exists with the following details
    | Name            | Description | Price | ProductType | Stock |
    | SearchItemAlpha | Test Alpha  |   100 | CPU         |     5 |
  When I search for products by name "SearchItemAlpha" in UI
  Then I see product "SearchItemAlpha" in the list
  And I do not see product "Ryzen 7 7800X3D" in the list

Scenario: Filter by type filters the product list
  When I filter products by type "MONITOR" in UI
  Then I see product "Dell UltraSharp 27" in the list
  And I do not see product "Ryzen 7 7800X3D" in the list
