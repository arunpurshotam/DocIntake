using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocIntake.Core.Abstractions;
using DocIntake.Infrastructure.Extraction;
using DocIntake.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace DocIntake.Tests.Api;

/// <summary>
/// End-to-end tests: the real app runs in memory and receives real multipart HTTP requests.
/// Each test gets its own app (and so its own queue and stores), so tests can't affect each other.
/// </summary>
public class DocumentsApiTests
{
    // Builds the same request as: curl -F "<fieldName>=@<fileName>" .../documents
    private static MultipartFormDataContent Form(string fieldName, string fileName, string text)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return new MultipartFormDataContent { { file, fieldName, fileName } };
    }

    private static MultipartFormDataContent Upload(string fileName, string text) => Form("file", fileName, text);

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string? Id(JsonElement body) => body.GetProperty("id").GetString();
    private static string? Status(JsonElement body) => body.GetProperty("status").GetString();

    // --- Upload: 202, queued, extractor not called ---

    [Fact]
    public async Task Upload_Returns202_WithLocation_AndDoesNotRunExtractor()
    {
        var extractor = new CountingExtractor(new FakeExtractor());
        using var factory = new DocIntakeApiFactory(configureServices: s => s.AddSingleton<IExtractor>(extractor));
        var client = factory.CreateClient();

        var response = await client.PostAsync("/documents", Upload("w2-good.txt", "synthetic wage statement"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("Received", Status(body));
        Assert.Equal(0, extractor.Calls);

        Assert.NotNull(response.Headers.Location);
        var fetched = await Json(await client.GetAsync(response.Headers.Location));
        Assert.Equal(Id(body), Id(fetched));
        Assert.Equal("Received", Status(fetched));

        // The upload queued the job; running it now completes the document.
        await factory.CreateWorker().ProcessNextAsync(CancellationToken.None);

        Assert.Equal(1, extractor.Calls);
        Assert.Equal("Approved", Status(await Json(await client.GetAsync(response.Headers.Location))));
    }

    // --- Idempotency ---

    [Fact]
    public async Task DuplicateUpload_ReturnsSameId_AndIsNotEnqueuedTwice()
    {
        var queue = new RecordingJobQueue();
        using var factory = new DocIntakeApiFactory(configureServices: s => s.AddSingleton<IJobQueue>(queue));
        var client = factory.CreateClient();

        var first = await client.PostAsync("/documents", Upload("w2-good.txt", "identical bytes"));
        // Same bytes under a different name: still the same document.
        var second = await client.PostAsync("/documents", Upload("copy-of-w2.txt", "identical bytes"));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var id = Id(await Json(first));
        Assert.Equal(id, Id(await Json(second)));
        Assert.Equal([Guid.Parse(id!)], queue.Enqueued);
    }

    [Fact]
    public async Task DifferentContent_GetsDifferentIds()
    {
        using var factory = new DocIntakeApiFactory();
        var client = factory.CreateClient();

        var a = await Json(await client.PostAsync("/documents", Upload("w2.txt", "content A")));
        var b = await Json(await client.PostAsync("/documents", Upload("w2.txt", "content B")));

        Assert.NotEqual(Id(a), Id(b));
    }

    // --- Dead letters ---

    [Fact]
    public async Task DeadLetteredDocument_AppearsInGetDeadLetters()
    {
        using var factory = new DocIntakeApiFactory();
        var client = factory.CreateClient();
        var upload = await client.PostAsync("/documents", Upload("w2-broken.txt", "synthetic"));
        var id = Id(await Json(upload));

        await factory.CreateWorker().ProcessNextAsync(CancellationToken.None);

        var deadLetters = await Json(await client.GetAsync("/dead-letters"));
        var deadLetter = Assert.Single(deadLetters.EnumerateArray());
        Assert.Equal(id, deadLetter.GetProperty("documentId").GetString());
        Assert.Equal(1, deadLetter.GetProperty("attempts").GetInt32());
        Assert.Contains("InvalidOperationException", deadLetter.GetProperty("error").GetString());
        Assert.Equal("Failed", Status(await Json(await client.GetAsync(upload.Headers.Location))));
    }

    [Fact]
    public async Task HostedWorker_ProcessesQueuedUploads_InTheBackground()
    {
        // The real BackgroundService runs here. Instead of sleeping, wait for the dead-letter
        // store to signal; the timeout is only a safety net so a bug can't hang the test run.
        var deadLetters = new SignalingDeadLetterStore();
        using var factory = new DocIntakeApiFactory(
            runHostedWorker: true,
            configureServices: s => s.AddSingleton<IDeadLetterStore>(deadLetters));
        var client = factory.CreateClient();

        var upload = await client.PostAsync("/documents", Upload("w2-broken.txt", "synthetic"));
        var added = await deadLetters.FirstAdded.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(Id(await Json(upload)), added.DocumentId.ToString());
        Assert.Single((await Json(await client.GetAsync("/dead-letters"))).EnumerateArray());
    }

    // --- Validation of the upload request (unchanged from Milestone 2) ---

    [Fact]
    public async Task Upload_EmptyFile_Returns400_SayingTheFileIsEmpty()
    {
        using var factory = new DocIntakeApiFactory();
        var response = await factory.CreateClient().PostAsync("/documents", Upload("empty.txt", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("The uploaded file is empty.", problem.GetProperty("title").GetString());
        Assert.Contains("empty.txt", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Upload_WrongFieldName_Returns400_NamingTheFieldReceived()
    {
        using var factory = new DocIntakeApiFactory();
        var response = await factory.CreateClient().PostAsync("/documents", Form("upload", "w2.txt", "synthetic"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("No file attached.", problem.GetProperty("title").GetString());
        Assert.Contains("'upload'", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Upload_NoFileInForm_Returns400_NoFileAttached()
    {
        using var factory = new DocIntakeApiFactory();
        var form = new MultipartFormDataContent { { new StringContent("w2.txt"), "file" } }; // curl -F "file=w2.txt" (no @)

        var response = await factory.CreateClient().PostAsync("/documents", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("No file attached.", (await Json(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Docs_AreServed_InDevelopment()
    {
        // WebApplicationFactory runs the app in the Development environment by default.
        using var factory = new DocIntakeApiFactory();
        var response = await factory.CreateClient().GetAsync("/docs/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
