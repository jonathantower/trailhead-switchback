namespace Switchback.Core.Repositories;

public static class TableStorageConstants
{
    /// <summary>Escapes a value for use inside an OData filter string literal (single quotes).</summary>
    public static string EscapeODataString(string? value) => value?.Replace("'", "''") ?? "";

    public const string TableUsers = "Users";
    public const string TableProviderConnections = "ProviderConnections";
    public const string TableRules = "Rules";
    public const string TableActivity = "Activity";
    public const string TableProcessedMessages = "ProcessedMessages";
    public const string TableUserEmail = "UserEmail";
    public const string TableGmailWatch = "GmailWatch";
}
