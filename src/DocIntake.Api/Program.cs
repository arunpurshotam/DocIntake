using System.Text.Json.Serialization;
using DocIntake.Api.BackgroundServices;
using DocIntake.Core.Abstractions;
using DocIntake.Core.Services;
using DocIntake.Core.Validation;
using DocIntake.Infrastructure.Extraction;
using DocIntake.Infrastructure.Queue;
using DocIntake.Infrastructure.Resilience;
using DocIntake.Infrastructure.Storage;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Singletons so every request and the worker share the same in-memory data.
builder.Services.AddSingleton<IDocumentStore, InMemoryDocumentStore>();
builder.Services.AddSingleton<IDocumentContentStore, InMemoryDocumentContentStore>();
builder.Services.AddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
builder.Services.AddSingleton<IJobQueue, ChannelJobQueue>();

// Pipeline services. All are thread-safe, so singletons are safe.
builder.Services.AddSingleton<IExtractor, FakeExtractor>();
builder.Services.AddSingleton<IValidationRule, RequiredFieldsRule>();
builder.Services.AddSingleton<IValidationRule, WithholdingRule>();
builder.Services.AddSingleton<Validator>();
builder.Services.AddSingleton<DocumentProcessor>();

// Retry settings come from "Processing:Retry" (see appsettings.json). Read when first resolved,
// so configuration overrides (e.g. zero delays in tests) are already applied.
builder.Services.AddSingleton<IExtractionRetryPolicy>(sp => new PollyExtractionRetryPolicy(
    sp.GetRequiredService<IConfiguration>().GetSection(ExtractionRetryOptions.SectionName).Get<ExtractionRetryOptions>()
    ?? new ExtractionRetryOptions()));

// Background worker that drains the job queue.
builder.Services.AddHostedService<ProcessingWorker>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // OpenAPI JSON at /openapi/v1.json, and an interactive docs page (Scalar) at /docs.
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options => options.WithTitle("DocIntake API"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
