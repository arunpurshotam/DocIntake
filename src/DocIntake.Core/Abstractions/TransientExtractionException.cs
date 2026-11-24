namespace DocIntake.Core.Abstractions;

/// <summary>
/// Thrown by an <see cref="IExtractor"/> for a temporary problem (timeout, throttling,
/// service briefly down) where trying again may succeed. Any other exception from an
/// extractor is treated as permanent and is not retried.
/// </summary>
public class TransientExtractionException : Exception
{
    public TransientExtractionException(string message) : base(message) { }

    public TransientExtractionException(string message, Exception innerException) : base(message, innerException) { }
}
