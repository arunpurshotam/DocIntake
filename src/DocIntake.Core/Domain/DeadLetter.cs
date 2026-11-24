namespace DocIntake.Core.Domain;

/// <summary>
/// A document that could not be processed, kept so a person can investigate or replay it.
/// </summary>
/// <param name="DocumentId">The document that failed.</param>
/// <param name="Error">The last error, as "ExceptionType: message".</param>
/// <param name="Attempts">How many extraction attempts were made, including the first.</param>
/// <param name="FailedAt">When the document was given up on.</param>
public record DeadLetter(Guid DocumentId, string Error, int Attempts, DateTimeOffset FailedAt);
