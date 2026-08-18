using System.Net;
using System.Text.Json;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal static class ApiTestAssertions
{
    public static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
    }

    public static async Task AssertValidationProblemDetailsAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.GetProperty("errors").EnumerateObject().Any());
    }
}
