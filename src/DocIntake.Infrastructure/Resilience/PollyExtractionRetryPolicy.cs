using DocIntake.Core.Abstractions;
using DocIntake.Core.Domain;
using Polly;
using Polly.Retry;

namespace DocIntake.Infrastructure.Resilience;

/// <summary>
/// Retries extraction with a Polly v8 resilience pipeline: exponential backoff, and ONLY for
/// <see cref="TransientExtractionException"/>. Any other exception passes straight through.
/// </summary>
public class PollyExtractionRetryPolicy : IExtractionRetryPolicy
{
    private readonly ResiliencePipeline<ExtractionResult> _pipeline;

    public PollyExtractionRetryPolicy(ExtractionRetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxRetryAttempts);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.BaseDelay, TimeSpan.Zero);

        var builder = new ResiliencePipelineBuilder<ExtractionResult>();
        if (options.MaxRetryAttempts > 0) // Polly requires at least 1; 0 means "no retries".
        {
            builder.AddRetry(new RetryStrategyOptions<ExtractionResult>
            {
                ShouldHandle = new PredicateBuilder<ExtractionResult>().Handle<TransientExtractionException>(),
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false, // predictable delays; add jitter when many workers share a dependency
            });
        }
        _pipeline = builder.Build();
    }

    public Task<ExtractionResult> ExecuteAsync(
        Func<CancellationToken, Task<ExtractionResult>> attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return _pipeline.ExecuteAsync(
            static async (attempt, token) => await attempt(token),
            attempt,
            cancellationToken).AsTask();
    }
}
