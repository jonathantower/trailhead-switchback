using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Switchback.Core.Repositories;
using Switchback.Core.Services;
using Switchback.Functions.Services;

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
            services.AddSingleton<IRuleRepository, TableRuleRepository>();
            services.AddSingleton<IActivityRepository, TableActivityRepository>();
            services.AddSingleton<IProcessedMessageRepository, TableProcessedMessageRepository>();
            services.AddSingleton<IUserEmailRepository, TableUserEmailRepository>();
            services.AddSingleton<IGmailWatchRepository, TableGmailWatchRepository>();
        }

        var kvUri = config["KeyVault:VaultUri"];
        var keyName = config["KeyVault:TokenEncryptionKeyName"] ?? "token-encryption-key";
        if (!string.IsNullOrEmpty(kvUri))
        {
            var keyId = new Uri($"{kvUri.TrimEnd('/')}/keys/{keyName}");
            services.AddSingleton(new CryptographyClient(keyId, new DefaultAzureCredential()));
            services.AddSingleton<IEncryptionService, KeyVaultEncryptionService>();
        }

        var openAiEndpoint = config["AzureOpenAI:Endpoint"]?.TrimEnd('/');
        var openAiKey = config["AzureOpenAI:ApiKey"];
        var openAiDeployment = config["AzureOpenAI:Deployment"] ?? config["AzureOpenAI:Model"] ?? "";
        if (!string.IsNullOrEmpty(openAiEndpoint) && !string.IsNullOrEmpty(openAiKey) && !string.IsNullOrEmpty(openAiDeployment))
            services.AddSingleton<IRuleClassifier>(sp => new AzureOpenAIRuleClassifier(sp.GetRequiredService<IHttpClientFactory>(), openAiEndpoint, openAiKey, openAiDeployment));
        else
            services.AddSingleton<IRuleClassifier, NoOpRuleClassifier>();

        services.AddSingleton<IAccessTokenProvider, ProviderConnectionAccessTokenProvider>();
        services.AddSingleton<IGmailMessageService, GmailMessageService>();
        services.AddSingleton<IM365MessageService, M365MessageService>();
        services.AddSingleton<EmailPipelineService>();

        services.AddHttpClient();
    })
    .Build();

await host.RunAsync();
