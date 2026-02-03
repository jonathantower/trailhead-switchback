using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        var connectionString = config["AzureWebJobsStorage"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddSingleton(new TableServiceClient(connectionString));
            services.AddSingleton<IUserRepository, TableUserRepository>();
            services.AddSingleton<IProviderConnectionRepository, TableProviderConnectionRepository>();
        }

        var kvUri = config["KeyVault:VaultUri"];
        var keyName = config["KeyVault:TokenEncryptionKeyName"] ?? "token-encryption-key";
        if (!string.IsNullOrEmpty(kvUri))
        {
            var keyId = new Uri($"{kvUri.TrimEnd('/')}/keys/{keyName}");
            services.AddSingleton(new CryptographyClient(keyId, new DefaultAzureCredential()));
            services.AddSingleton<IEncryptionService, KeyVaultEncryptionService>();
        }

        services.AddHttpClient();
    })
    .Build();

await host.RunAsync();
