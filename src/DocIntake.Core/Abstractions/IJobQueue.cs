namespace DocIntake.Core.Abstractions;

/// <summary>Hands document ids from the upload endpoint to the background worker.</summary>
public interface IJobQueue
{
    Task EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Waits until an id is available, then returns it. Each id is delivered to one caller.</summary>
    Task<Guid> DequeueAsync(CancellationToken cancellationToken);
}
