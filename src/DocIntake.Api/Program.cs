using System.Text.Json.Serialization;
using DocIntake.Core;
using DocIntake.Core.Validation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Singleton so every request shares the same in-memory data.
builder.Services.AddSingleton<IDocumentStore, InMemoryDocumentStore>();

// Pipeline services. All are stateless, so singletons are safe.
builder.Services.AddSingleton<IExtractor, FakeExtractor>();
builder.Services.AddSingleton<IValidationRule, RequiredFieldsRule>();
builder.Services.AddSingleton<IValidationRule, WithholdingRule>();
builder.Services.AddSingleton<Validator>();
builder.Services.AddSingleton<DocumentProcessor>();
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
