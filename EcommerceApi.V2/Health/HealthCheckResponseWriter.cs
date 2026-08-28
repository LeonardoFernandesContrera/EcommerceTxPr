using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EcommerceApi.V2.Health;

internal static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description
                    },
                    StringComparer.Ordinal)
        };

        await JsonSerializer.SerializeAsync(
                context.Response.Body,
                response,
                SerializerOptions,
                context.RequestAborted)
            .ConfigureAwait(false);
    }
}
