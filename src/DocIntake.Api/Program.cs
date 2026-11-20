using System.Text.Json.Serialization;
using DocIntake.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Singleton so every request shares the same in-memory data.
builder.Services.AddSingleton<IDocumentStore, InMemoryDocumentStore>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
