namespace DocIntake.Infrastructure.Resilience;

/// <summary>
/// Retry settings for extraction, bound from the "Processing:Retry" configuration section.
/// With the defaults, retries wait 1s, 2s, 4s (exponential backoff, no jitter).
/// Tests set <see cref="BaseDelay"/> to zero so retries happen immediately.
/// </summary>
public class ExtractionRetryOptions
{
    public const string SectionName = "Processing:Retry";

    /// <summary>Retries after the first attempt (so up to MaxRetryAttempts + 1 attempts in total).</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Delay before the first retry; each later retry doubles it.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}
