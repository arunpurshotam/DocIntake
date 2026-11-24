using DocIntake.Api.BackgroundServices;
using DocIntake.Core.Abstractions;
using DocIntake.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocIntake.Tests.Api;

/// <summary>
/// Runs the real app in memory with zero retry delays.
/// By default the hosted <see cref="ProcessingWorker"/> is removed, so a test decides exactly
/// when a job runs by calling <see cref="CreateWorker"/>().ProcessNextAsync(...): no sleeps, no races.
/// Pass runHostedWorker: true to test the real background service instead.
/// </summary>
public sealed class DocIntakeApiFactory(
    bool runHostedWorker = false,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Processing:Retry:BaseDelay", "00:00:00");

        builder.ConfigureTestServices(services =>
        {
            if (!runHostedWorker)
            {
                services.Remove(services.Single(s =>
                    s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(ProcessingWorker)));
            }

            configureServices?.Invoke(services); // later registrations replace the app's
        });
    }

    /// <summary>A worker wired to the app's own services, for running one job at a time.</summary>
    public ProcessingWorker CreateWorker() => new(
        Services.GetRequiredService<IJobQueue>(),
        Services.GetRequiredService<IDocumentStore>(),
        Services.GetRequiredService<DocumentProcessor>(),
        NullLogger<ProcessingWorker>.Instance);
}
