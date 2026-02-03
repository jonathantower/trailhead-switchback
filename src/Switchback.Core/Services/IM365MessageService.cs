namespace Switchback.Core.Services;

/// <summary>
/// Fetches M365 message content and moves to folder.
/// </summary>
public interface IM365MessageService
{
    /// <summary>Fetches from, subject, and body snippet (caller truncates).</summary>
    Task<(string From, string Subject, string BodySnippet)?> FetchMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken = default);

    /// <summary>Moves message to folder by display name. Looks up folder id via mailFolders.</summary>
    Task<bool> MoveToFolderAsync(string accessToken, string messageId, string folderDisplayName, CancellationToken cancellationToken = default);
}
