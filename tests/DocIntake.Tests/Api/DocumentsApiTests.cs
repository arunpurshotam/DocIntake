using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocIntake.Tests.Api;

/// <summary>
/// End-to-end tests: the real app runs in memory and receives real multipart HTTP requests,
/// so routing, form binding and JSON serialization are all exercised.
/// </summary>
public class DocumentsApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Builds the same request as: curl -F "<fieldName>=@<fileName>" .../documents
    private static MultipartFormDataContent Form(string fieldName, string fileName, string text)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return new MultipartFormDataContent { { file, fieldName, fileName } };
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Upload_SmallFile_Returns201_AndDocumentCanBeFetched()
    {
        var response = await _client.PostAsync("/documents", Form("file", "w2-good.txt", "synthetic wage statement"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json(response);
        var id = body.GetProperty("id").GetString();
        Assert.Equal("Approved", body.GetProperty("status").GetString());

        Assert.NotNull(response.Headers.Location);
        var fetched = await Json(await _client.GetAsync(response.Headers.Location));
        Assert.Equal(id, fetched.GetProperty("id").GetString());
        Assert.Equal("w2-good.txt", fetched.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400_SayingTheFileIsEmpty()
    {
        var response = await _client.PostAsync("/documents", Form("file", "empty.txt", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("The uploaded file is empty.", problem.GetProperty("title").GetString());
        Assert.Contains("empty.txt", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Upload_WrongFieldName_Returns400_NamingTheFieldReceived()
    {
        var response = await _client.PostAsync("/documents", Form("upload", "w2.txt", "synthetic"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("No file attached.", problem.GetProperty("title").GetString());
        Assert.Contains("'upload'", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Upload_NoFileInForm_Returns400_NoFileAttached()
    {
        var form = new MultipartFormDataContent { { new StringContent("w2.txt"), "file" } }; // curl -F "file=w2.txt" (no @)

        var response = await _client.PostAsync("/documents", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("No file attached.", (await Json(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Docs_AreServed_InDevelopment()
    {
        // WebApplicationFactory runs the app in the Development environment by default.
        var response = await _client.GetAsync("/docs/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
