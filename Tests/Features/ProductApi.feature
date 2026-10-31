@api @local
Feature: Product API

Scenario: Get an existing product
  Given a product exists with the following details
    | Name        | Description         | Price | ProductType |
    | API Get Ref | Created by API tests |   399 | CPU         |
  When I request the created product
  Then the response status is 200
  And the product has name "API Get Ref" and price 399

Scenario: Delete a product
  Given a product exists with the following details
    | Name         | Description          | Price | ProductType |
    | API Delete 1 | Created by API tests |    10 | CPU         |
  When I delete the created product
  Then the response status is 204
  When I request the created product
  Then the response status is 404

Scenario: POST with missing Name returns 400
  When I create a product with missing name
  Then the response status is 400

Scenario: POST with Name longer than 100 characters returns 400
  When I create a product with a name longer than 100 characters
  Then the response status is 400

Scenario: POST with negative Price returns 400
  When I create a product with negative price -1
  Then the response status is 400

Scenario: POST duplicate name returns 409
  Given a product exists with the following details
    | Name       | Description      | Price | ProductType |
    | API Dupe 1 | Original product |   100 | CPU         |
  When I create product with following details via API
    | Name       | Description       | Price | ProductType |
    | API Dupe 1 | Duplicate product |   150 | CPU         |
  Then the response status is 409
  And the response detail contains "API Dupe 1"

Scenario: PUT duplicate name returns 409
  Given a product exists with the following details
    | Name       | Description   | Price | ProductType |
    | API Dupe 2 | First product |   100 | CPU         |
  And another product exists with the following details
    | Name       | Description    | Price | ProductType |
    | API Dupe 3 | Second product |   200 | MONITOR     |
  When I update the second product name to "API Dupe 2"
  Then the response status is 409
  And the response detail contains "API Dupe 2"

Scenario: PUT same name on same product returns 204
  Given a product exists with the following details
    | Name       | Description       | Price | ProductType |
    | API Put 1  | Put same name     |   100 | CPU         |
  When I update the created product with the same name
  Then the response status is 204

Scenario: GET with search filter returns only matching products
  Given a product exists with the following details
    | Name              | Description  | Price | ProductType |
    | UniqueSearchTagX1 | Search test  |   100 | CPU         |
  When I search for products with name "UniqueSearchTagX1"
  Then the response status is 200
  And the product list contains "UniqueSearchTagX1"

Scenario: GET with type filter returns only products of that type
  When I filter products by type "MONITOR"
  Then the response status is 200
  And all products in the response have type "MONITOR"

Scenario: GET with invalid type returns 400
  When I filter products by type "INVALID_TYPE"
  Then the response status is 400

Scenario: Stock field is persisted and returned
  Given a product exists with the following details
    | Name         | Description | Price | ProductType | Stock |
    | API Stock 1  | Stock test  |   100 | CPU         |     7 |
  When I request the created product
  Then the response status is 200
  And the product has stock 7

Scenario Outline: POST rejects out-of-range price and stock values
  When I POST a product named "ValidationRange" with price <price> and stock <stock>
  Then the response status is 400

  Examples:
    | price | stock |
    |    -1 |     1 |
    |   100 |    -1 |
    |    -1 |    -1 |

Scenario Outline: GET rejects type filters that are not ProductType values
  When I filter products by type "<type>"
  Then the response status is 400

  Examples:
    | type         |
    | INVALID_TYPE |
    | UNKNOWN      |
    | MONITOR_LIST |

Scenario Outline: PUT with a body id that does not match the route returns 400
  Given a product exists with the following details
    | Name            | Description | Price | ProductType | Stock |
    | API Mismatch Id | Id mismatch |   100 | CPU         |     1 |
  When I PUT the created product with a body id offset by <offset>
  Then the response status is 400

  Examples:
    | offset |
    |      1 |
    |     -1 |
    |   1000 |

Scenario: Search and type filters combine with AND semantics
  Given a product exists with the following details
    | Name        | Description | Price | ProductType | Stock |
    | ComboFilter | Combo test  |   100 | CPU         |     1 |
  When I search for products with name "ComboFilter" and type "CPU"
  Then the response status is 200
  And the product list contains "ComboFilter"
  When I search for products with name "ComboFilter" and type "MONITOR"
  Then the response status is 200
  And the product list does not contain "ComboFilter"
