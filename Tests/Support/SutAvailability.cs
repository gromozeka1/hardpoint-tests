using HardPoint.Testing.Config;
using Reqnroll;

namespace Tests.Support;

/// <summary>
/// Fail-fast guard for the whole run: probes the SUT endpoints once before any
/// scenario executes and fails immediately with a single actionable message when
/// they are unreachable, instead of every scenario failing with
/// connection-refused noise.
/// Implemented as a Reqnroll <c>BeforeTestRun</c> hook because MSTest allows
/// only one <c>[AssemblyInitialize]</c> per assembly and the MSTest source
/// generator already owns it (build error UTA013 otherwise).
/// </summary>
[Binding]
public static class SutAvailability
{
    private const int _attempts = 6;
    private static readonly TimeSpan _delayBetweenAttempts = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(5);

    [BeforeTestRun]
    public static async Task VerifySutIsReachable()
    {
        TestSettings settings = ConfigReader.Settings;

        await ProbeAsync("WebApp", settings.ApplicationUrl);

        var apiBaseUrl = settings.APIRequestNewContextOptions.BaseURL;
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException(
                "apiOptions:BaseURL is not configured, so the ProductAPI cannot be probed. " +
                "Set it in Tests/appsettings.json or via TEST_APIRequestNewContextOptions__BaseURL.");
        }

        await ProbeAsync("ProductAPI", apiBaseUrl.TrimEnd('/') + "/products");
    }

    private static async Task ProbeAsync(string componentName, string url)
    {
        using HttpClient client = new() { Timeout = _requestTimeout };
        Exception? lastError = null;

        for (var attempt = 1; attempt <= _attempts; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                lastError = new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }

            if (attempt < _attempts)
            {
                await Task.Delay(_delayBetweenAttempts);
            }
        }

        throw new InvalidOperationException(
            $"SUT is not reachable: {componentName} did not answer GET {url} " +
            $"within {_attempts} attempts (last error: {lastError?.Message}). " +
            "Start the SUT before running tests with 'docker compose up -d' — see README.md.");
    }
}
