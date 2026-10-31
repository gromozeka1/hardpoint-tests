@api @local
Feature: Product API contract

  The ProductAPI publishes an OpenAPI document. These scenarios check that the document still
  describes what this suite depends on, and that the responses it reads conform to the schemas
  the document declares — so a contract change between image releases fails here, with a schema
  mismatch, instead of surfacing later as a confusing UI failure.

  Scenario: The document describes the endpoints and the product schema
    Given the ProductAPI publishes an OpenAPI document
    Then it describes the Product schema that the test model mirrors
    And a payload that omits the required name does not conform to the Product schema
    And it describes ProductType as a number
    And it exposes "get" on "/api/Products"
    And it exposes "post" on "/api/Products"
    And it exposes "get" on "/api/Products/{id}"
    And it exposes "put" on "/api/Products/{id}"
    And it exposes "delete" on "/api/Products/{id}"

  Scenario: A product list response conforms to the published schema
    Given the ProductAPI publishes an OpenAPI document
    Then the product list response matches the Product schema

  Scenario: A single product response conforms to the published schema
    Given a product exists with the following details
      | Name        | Description | Price | ProductType | Stock |
      | ContractOne | Schema test |   100 | CPU         |     3 |
    And the ProductAPI publishes an OpenAPI document
    Then the created product response matches the Product schema
