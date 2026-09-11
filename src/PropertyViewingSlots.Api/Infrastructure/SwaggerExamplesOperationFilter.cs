using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PropertyViewingSlots.Api.Infrastructure;

public sealed class SwaggerExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var relativePath = context.ApiDescription.RelativePath;

        if (string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(relativePath, "api/viewings", StringComparison.OrdinalIgnoreCase) &&
            operation.RequestBody?.Content.TryGetValue("application/json", out var requestBody) == true)
        {
            requestBody.Example = new OpenApiObject
            {
                ["propertyId"] = new OpenApiString("property-uk-123"),
                ["userId"] = new OpenApiString("user-456"),
                ["startTime"] = new OpenApiString("2026-09-15T09:00:00Z")
            };
        }

        if (string.Equals(context.ApiDescription.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(relativePath, "api/properties/{propertyId}/available-viewing-slots", StringComparison.OrdinalIgnoreCase))
        {
            SetParameterExample(operation, "propertyId", "property-uk-123");
            SetParameterExample(operation, "from", "2026-09-15");
            SetParameterExample(operation, "to", "2026-09-15");
        }
    }

    private static void SetParameterExample(OpenApiOperation operation, string parameterName, string example)
    {
        var parameter = operation.Parameters.FirstOrDefault(item =>
            string.Equals(item.Name, parameterName, StringComparison.Ordinal));

        if (parameter?.Schema is not null)
        {
            parameter.Schema.Example = new OpenApiString(example);
        }
    }
}
