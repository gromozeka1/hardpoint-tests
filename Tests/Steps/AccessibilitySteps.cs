using AwesomeAssertions;
using HardPoint.Testing.Config;
using HardPoint.Testing.Driver;
using Microsoft.Playwright;
using Reqnroll;
using Tests.Support;

namespace Tests.Steps;

/// <summary>
/// Accessibility scans of the pages the suite already drives, using axe-core.
/// </summary>
/// <remarks>
/// The assertions are deliberately two-part: <em>critical</em> violations fail the scenario outright,
/// while the total count is pinned as a regression ceiling. The WebApp is a demo application with
/// known lower-severity findings, so demanding zero of everything would mean either a permanently
/// red suite or a rule set narrowed until it proves nothing.
/// </remarks>
[Binding]
public sealed class AccessibilitySteps(Accessibility accessibility, PlaywrightDriver playwrightDriver)
{
    private IReadOnlyList<AccessibilityViolation> _violations = [];

    [When("I open the product list page")]
    public async Task WhenIOpenTheProductListPage()
    {
        IPage page = await playwrightDriver.GetPageAsync();
        await page.GotoAsync($"{ConfigReader.Settings.ApplicationUrl.TrimEnd('/')}/Products");
    }

    /// <summary>
    /// A page with a deliberate, well-known defect (an image with no alt text), used to prove the
    /// scanner and its rule set actually run — a clean result from a scanner that never fired would
    /// otherwise look identical to a genuinely clean page.
    /// </summary>
    [When("I open a page with a known accessibility defect")]
    public async Task WhenIOpenAPageWithAKnownAccessibilityDefect()
    {
        IPage page = await playwrightDriver.GetPageAsync();
        await page.SetContentAsync(
            """<html lang="en"><head><title>Known defect</title></head><body><img src="data:," width="10" height="10"></body></html>""");
    }

    [Then("the scan reports the {string} violation")]
    public void ThenTheScanReportsTheViolation(string ruleId)
    {
        _ = _violations.Select(violation => violation.Id).Should().Contain(ruleId,
            "the scanner must be able to detect a defect it is pointed at; without this, a clean scan " +
            "of the application would prove nothing. Reported: {0}",
            Describe(_violations));
    }

    [When("I check the page for accessibility violations")]
    public async Task WhenICheckThePageForAccessibilityViolations()
        => _violations = await accessibility.ScanAsync();

    [Then("there are no {string} accessibility violations")]
    public void ThenThereAreNoAccessibilityViolationsOfImpact(string impact)
    {
        AccessibilityViolation[] matching = [.. _violations.Where(violation => string.Equals(violation.Impact, impact, StringComparison.OrdinalIgnoreCase))];

        _ = matching.Should().BeEmpty(
            "axe reported {0} violation(s) of impact '{1}'; all findings: {2}",
            matching.Length,
            impact,
            Describe(_violations));
    }

    [Then("the page reports at most {int} accessibility violations")]
    public void ThenThePageReportsAtMostAccessibilityViolations(int maximum)
    {
        _ = _violations.Count.Should().BeLessThanOrEqualTo(maximum,
            "the violation count is a regression ceiling: lower it when findings are fixed, and never raise it " +
            "without a reason in the scenario. Reported: {0}",
            Describe(_violations));
    }

    private static string Describe(IReadOnlyList<AccessibilityViolation> violations)
        => violations.Count == 0
            ? "none"
            : string.Join("; ", violations.Select(violation => violation.ToString()));
}
