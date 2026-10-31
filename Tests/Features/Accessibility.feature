@ui @local
Feature: Accessibility

  axe-core checks against the pages the suite already drives. Critical violations fail the
  scenario; the total is pinned as a regression ceiling so a new finding shows up as a failing
  scenario instead of passing silently.

  Scenario: The scanner detects a known defect
    When I open a page with a known accessibility defect
    And I check the page for accessibility violations
    Then the scan reports the "image-alt" violation

  Scenario: The product list page has no critical accessibility violations
    When I open the product list page
    And I check the page for accessibility violations
    Then there are no "critical" accessibility violations
    And the page reports at most 0 accessibility violations

  Scenario: The create product form has no critical accessibility violations
    Given I click the Product menu
    When I check the page for accessibility violations
    Then there are no "critical" accessibility violations
    And the page reports at most 0 accessibility violations
