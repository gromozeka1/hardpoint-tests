using System.Text.Json;
using System.Text.Json.Nodes;
using HardPoint.Testing.Config;
using HardPoint.Testing.Driver;
using Json.Schema;
using Microsoft.Playwright;

namespace Tests.Support;

/// <summary>
/// The contract the ProductAPI publishes about itself, and the schemas in it.
/// </summary>
/// <remarks>
/// The document is fetched from the running service (<c>GET /openapi/v1.json</c>, generated in the
/// Development environment) instead of being copied out of the SUT repository: that way a schema
/// change is validated against the image actually under test, and the suite cannot silently keep
/// describing an older release.
/// </remarks>
public class ApiContract(PlaywrightDriver playwrightDriver)
{
    private const string _documentPath = "openapi/v1.json";

    private readonly Dictionary<string, JsonObject> _declaredSchemas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonSchema> _compiledSchemas = new(StringComparer.Ordinal);
    private JsonObject? _document;

    public async Task<JsonObject> GetDocumentAsync()
    {
        if (_document is not null)
        {
            return _document;
        }

        // apiOptions points at .../api/, while the document is served from the origin root.
        var baseUrl = ConfigReader.Settings.APIRequestNewContextOptions.BaseURL
            ?? throw new InvalidOperationException(
                "apiOptions:BaseURL is not configured, so the OpenAPI document cannot be located. " +
                "Set it in Tests/appsettings.json or via TEST_APIRequestNewContextOptions__BaseURL.");

        var documentUrl = new Uri(new Uri(baseUrl, UriKind.Absolute), $"/{_documentPath}");
        IAPIResponse response = await (await playwrightDriver.ApiRequestContext).GetAsync(documentUrl.AbsoluteUri);

        if (response.Status != 200)
        {
            throw new InvalidOperationException(
                $"The ProductAPI publishes no OpenAPI document: GET {documentUrl.AbsoluteUri} returned HTTP {response.Status}. " +
                "The document is only generated in the Development environment, which docker-compose.yml sets. " +
                "If the service is started another way, set ASPNETCORE_ENVIRONMENT=Development.");
        }

        return _document = JsonNode.Parse(await response.TextAsync()) as JsonObject
            ?? throw new InvalidOperationException($"GET {documentUrl.AbsoluteUri} did not return a JSON object.");
    }

    /// <summary>
    /// The schema declaration for <paramref name="name"/>, with <c>$ref</c> links from
    /// <c>components.schemas</c> inlined so it can be inspected and compiled on its own.
    /// </summary>
    public async Task<JsonObject> GetDeclaredSchemaAsync(string name)
    {
        if (_declaredSchemas.TryGetValue(name, out JsonObject? cached))
        {
            return cached;
        }

        JsonObject document = await GetDocumentAsync();
        JsonObject declarations = document["components"]?["schemas"] as JsonObject
            ?? throw new InvalidOperationException("The OpenAPI document declares no components.schemas.");

        JsonNode declaration = declarations[name]
            ?? throw new InvalidOperationException(
                $"The OpenAPI document declares no schema named '{name}'. " +
                $"Declared schemas: {string.Join(", ", declarations.Select(entry => entry.Key))}.");

        JsonObject resolved = ResolveReferences(declaration, declarations) as JsonObject
            ?? throw new InvalidOperationException($"The OpenAPI schema '{name}' is not a JSON object.");

        _declaredSchemas[name] = resolved;
        return resolved;
    }

    public async Task<JsonSchema> GetCompiledSchemaAsync(string name)
    {
        if (_compiledSchemas.TryGetValue(name, out JsonSchema? cached))
        {
            return cached;
        }

        JsonObject schema = await GetDeclaredSchemaAsync(name);
        JsonSchema compiled = JsonSchema.FromText(schema.ToJsonString());
        _compiledSchemas[name] = compiled;
        return compiled;
    }

    /// <summary>A list of that schema, as the collection endpoints return it.</summary>
    public async Task<JsonSchema> GetArraySchemaAsync(string name)
    {
        var arraySchemaName = $"{name}[]";
        if (_compiledSchemas.TryGetValue(arraySchemaName, out JsonSchema? cached))
        {
            return cached;
        }

        JsonObject itemSchema = await GetDeclaredSchemaAsync(name);
        var arraySchema = new JsonObject
        {
            ["type"] = "array",
            ["items"] = JsonNode.Parse(itemSchema.ToJsonString())
        };

        JsonSchema compiled = JsonSchema.FromText(arraySchema.ToJsonString());
        _compiledSchemas[arraySchemaName] = compiled;
        return compiled;
    }

    /// <summary>
    /// Validates <paramref name="instance"/> and returns <see langword="null"/> when it conforms,
    /// or a readable list of the paths that failed.
    /// </summary>
    public async Task<string?> FindMismatchAsync(JsonNode instance, JsonSchema schema)
    {
        EvaluationResults results = schema.Evaluate(JsonSerializer.SerializeToElement(instance), new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        if (results.IsValid)
        {
            return null;
        }

        var problems = new List<string>();
        CollectProblems(results, problems);

        return problems.Count > 0
            ? string.Join("; ", problems.Distinct(StringComparer.Ordinal))
            : "the response does not conform to the schema (no detail was reported)";
    }

    private static void CollectProblems(EvaluationResults results, List<string> problems)
    {
        if (!results.IsValid && results.Errors is { Count: > 0 } errors)
        {
            var location = string.IsNullOrEmpty(results.InstanceLocation.ToString())
                ? "(root)"
                : results.InstanceLocation.ToString();

            problems.AddRange(errors.Select(error => $"{location}: {error.Key} — {error.Value}"));
        }

        if (results.Details is null)
        {
            return;
        }

        foreach (EvaluationResults detail in results.Details)
        {
            CollectProblems(detail, problems);
        }
    }

    private static JsonNode ResolveReferences(JsonNode node, JsonObject declarations)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject["$ref"] is JsonValue reference
                && reference.TryGetValue<string>(out string? target)
                && target.StartsWith("#/components/schemas/", StringComparison.Ordinal))
            {
                var schemaName = target["#/components/schemas/".Length..];

                return declarations[schemaName] is JsonObject referenced
                    ? ResolveReferences(referenced.DeepClone(), declarations)
                    : throw new InvalidOperationException($"The OpenAPI document references an undeclared schema: {target}.");
            }

            var resolvedObject = new JsonObject();
            foreach ((string key, JsonNode? value) in jsonObject.ToList())
            {
                resolvedObject[key] = value is null ? null : ResolveReferences(value, declarations);
            }

            return resolvedObject;
        }

        if (node is JsonArray jsonArray)
        {
            var resolvedArray = new JsonArray();
            foreach (JsonNode? item in jsonArray.ToList())
            {
                resolvedArray.Add(item is null ? null : ResolveReferences(item, declarations));
            }

            return resolvedArray;
        }

        return node.DeepClone();
    }
}
