using System.Text.Json;
using HardPoint.Testing.Driver;
using Microsoft.Playwright;

namespace Tests.Support;

/// <summary>
/// Runs axe-core against the page a scenario has open and summarises what it finds.
/// </summary>
/// <remarks>
/// The axe-core bundle is vendored at <c>Tests/Support/Axe/axe.min.js</c> (MPL-2.0, pinned version)
/// and injected with <c>AddScriptTag</c> rather than loaded from a CDN, so a scan never depends on
/// network access to a third party and the rule set only changes when the file is updated.
/// </remarks>
public class Accessibility(PlaywrightDriver playwrightDriver)
{
    private const string _scanScript = """
        async () => await axe.run(document, {
            resultTypes: ['violations'],
            runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice']
        })
        """;

    public async Task<IReadOnlyList<AccessibilityViolation>> ScanAsync()
    {
        IPage page = await playwrightDriver.GetPageAsync();
        var bundle = Path.Combine(AppContext.BaseDirectory, "Support", "Axe", "axe.min.js");

        if (!File.Exists(bundle))
        {
            throw new InvalidOperationException(
                $"The axe-core bundle was not found at '{bundle}'. It is copied to the output directory " +
                "from Tests/Support/Axe by Tests.csproj; restore it with 'dotnet build' before running UI scans.");
        }

        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Path = bundle });

        JsonElement results = await page.EvaluateAsync<JsonElement>(_scanScript);
        var violations = new List<AccessibilityViolation>();

        foreach (JsonElement violation in results.GetProperty("violations").EnumerateArray())
        {
            violations.Add(new AccessibilityViolation(
                Id: violation.GetProperty("id").GetString() ?? "unknown-rule",
                Impact: violation.TryGetProperty("impact", out JsonElement impact) ? impact.GetString() ?? "unknown" : "unknown",
                Help: violation.TryGetProperty("help", out JsonElement help) ? help.GetString() ?? string.Empty : string.Empty,
                NodeCount: violation.TryGetProperty("nodes", out JsonElement nodes) ? nodes.GetArrayLength() : 0));
        }

        return violations;
    }
}

/// <summary>One axe-core rule violation, summarised for an assertion message.</summary>
public sealed record AccessibilityViolation(string Id, string Impact, string Help, int NodeCount)
{
    public override string ToString() => $"[{Impact}] {Id} on {NodeCount} element(s) — {Help}";
}
