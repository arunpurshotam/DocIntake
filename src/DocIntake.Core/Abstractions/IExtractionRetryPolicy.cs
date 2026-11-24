using DocIntake.Core.Domain;

namespace DocIntake.Core.Abstractions;

/// <summary>
/// Decides whether and when a failed extraction attempt is retried.
/// Lets Core stay free of any particular resilience library (the real one uses Polly).
/// </summary>
public interface IExtractionRetryPolicy
{
    /// <summary>
    /// Runs <paramref name="attempt"/>, retrying it as the policy allows. If the last attempt
    /// fails, its exception is rethrown.
    /// </summary>
    Task<ExtractionResult> ExecuteAsync(
        Func<CancellationToken, Task<ExtractionResult>> attempt,
        CancellationToken cancellationToken);
}
