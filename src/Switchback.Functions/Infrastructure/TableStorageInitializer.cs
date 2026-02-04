using Azure.Data.Tables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Switchback.Core.Repositories;

namespace Switchback.Functions.Infrastructure;

/// <summary>
/// Ensures all Table Storage tables exist when the Functions host starts (e.g. local or first deploy).
/// </summary>
public sealed class TableStorageInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TableStorageInitializer> _logger;

    private static readonly string[] TableNames =
    {
        TableStorageConstants.TableUsers,
        TableStorageConstants.TableProviderConnections,
        TableStorageConstants.TableRules,
        TableStorageConstants.TableActivity,
        TableStorageConstants.TableProcessedMessages,
        TableStorageConstants.TableUserEmail,
        TableStorageConstants.TableGmailWatch
    };

    public TableStorageInitializer(IServiceProvider services, ILogger<TableStorageInitializer> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = _services.GetService(typeof(TableServiceClient)) as TableServiceClient;
        if (client == null)
        {
            _logger.LogDebug("TableServiceClient not configured; skipping table creation");
            return;
        }

        foreach (var name in TableNames)
        {
            try
            {
                var table = client.GetTableClient(name);
                await table.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Table {TableName} exists or was created", name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create table {TableName}", name);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
