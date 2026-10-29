using Reqnroll;

namespace Tests.Support;

/// <summary>
/// Names the products a scenario creates uniquely, so scenarios running in
/// parallel — or a run against a database left dirty by an earlier one — can
/// never create, find, or delete each other's data.
/// </summary>
/// <remarks>
/// Feature files keep using stable, readable names. Steps call <see cref="Register"/>
/// when they create a product and <see cref="Actual"/> when they refer to one, so
/// every step of a scenario agrees on the same suffixed name. Names a scenario did
/// not create (seed data) are passed through untouched.
/// </remarks>
public sealed class ProductNames(ScenarioContext scenarioContext)
{
    private const int MaxNameLength = 100;
    private const string SuffixKey = "ProductNameSuffix";
    private const string KnownNamesKey = "KnownProductNames";

    /// <summary>
    /// Returns the unique name of a product the scenario is about to create,
    /// remembering it so later references resolve to the same product.
    /// </summary>
    public string Register(string logicalName)
    {
        Dictionary<string, string> known = KnownNames;

        if (known.TryGetValue(logicalName, out string? existing))
        {
            return existing;
        }

        var suffix = $" {Suffix}";
        var prefix = logicalName.Length + suffix.Length > MaxNameLength
            ? logicalName[..(MaxNameLength - suffix.Length)]
            : logicalName;

        var uniqueName = prefix + suffix;
        known[logicalName] = uniqueName;
        return uniqueName;
    }

    /// <summary>
    /// Maps a name written in a feature file to the product this scenario created
    /// under it, leaving names the scenario did not create unchanged.
    /// </summary>
    public string Actual(string name) => KnownNames.TryGetValue(name, out string? actual) ? actual : name;

    /// <summary>
    /// Applies the registered names to a message that quotes one, such as a
    /// duplicate-name error raised by the SUT.
    /// </summary>
    public string ActualIn(string text)
    {
        foreach ((string logical, string actual) in KnownNames.OrderByDescending(pair => pair.Key.Length))
        {
            text = text.Replace(logical, actual, StringComparison.Ordinal);
        }

        return text;
    }

    private string Suffix
    {
        get
        {
            if (!scenarioContext.TryGetValue<string>(SuffixKey, out string? suffix) || suffix is null)
            {
                suffix = Guid.NewGuid().ToString("N")[..8];
                scenarioContext.Set(suffix, SuffixKey);
            }

            return suffix;
        }
    }

    private Dictionary<string, string> KnownNames
    {
        get
        {
            if (!scenarioContext.TryGetValue<Dictionary<string, string>>(KnownNamesKey, out Dictionary<string, string>? known) || known is null)
            {
                known = [];
                scenarioContext.Set(known, KnownNamesKey);
            }

            return known;
        }
    }
}
