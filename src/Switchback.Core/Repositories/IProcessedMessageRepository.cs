using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public interface IProcessedMessageRepository
{
    /// <summary>Returns true if the message was already processed (idempotency check).</summary>
    Task<bool> ExistsAsync(string provider, string messageId, CancellationToken cancellationToken = default);
    /// <summary>Marks message as processed. Call before applying action to prevent duplicate processing.</summary>
    Task MarkProcessedAsync(string provider, string messageId, CancellationToken cancellationToken = default);
}
