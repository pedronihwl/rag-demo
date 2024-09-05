using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using EmbedFunction;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using Shared.Extensions;
using Shared.Options;
using Shared.Serializer;
using Shared.Services;

var credential = new DefaultAzureCredential();

var builder = new HostBuilder();

builder.ConfigureAppConfiguration((context, config) =>
{
    config.ConfigureAzureKeyVault(credential);
});

builder.ConfigureServices((context, services) =>
{
    IConfiguration configuration = context.Configuration;

    services.AddLogging();

    services.AddApplicationInsightsTelemetryWorkerService();
    services.ConfigureFunctionsApplicationInsights();

    services.AddAzureClients(opt =>
    {
        opt.AddDocumentAnalysisClient(
            new Uri("https://cog-formreconizer.cognitiveservices.azure.com/"),
            new AzureKeyCredential("846e4b12958f4af9b795189a1dd1934c"));
    });

    services.Configure<CosmosDbOptions>(configuration.GetSection("azureServiceOptions"));

    services.AddSingleton<EmbedService>();

    services.AddSingleton<CosmosClient>(prov =>
    {
        var config = prov.GetRequiredService<IConfiguration>();
        var azureCosmosDbEndpoint = config["azureCosmosEndpoint"];
        ArgumentException.ThrowIfNullOrEmpty(azureCosmosDbEndpoint);
        
        var azureCosmosDbToken = config["azureCosmosToken"];
        ArgumentException.ThrowIfNullOrEmpty(azureCosmosDbToken);
        
        return new CosmosClientBuilder(azureCosmosDbEndpoint, azureCosmosDbToken)
            .WithCustomSerializer(new CosmosSystemTextJsonSerializer(new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            }))
            .Build();
    });

    services.AddSingleton<BlobServiceClient>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var azureStorageToken = config["stAccountToken"];
        ArgumentException.ThrowIfNullOrEmpty(azureStorageToken);
        return new BlobServiceClient(azureStorageToken);
    });

    services.AddSingleton<BlobContainerClient>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var azureStorageContainer = config["stAccountContainer"];
        return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(azureStorageContainer);
    });

    services.AddSingleton<AzureEmbedService>(sp =>
    {
        var model = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_DEPLOYMENT") 
            ?? throw new ArgumentException("env OPENAI_EMBEDDING_DEPLOYMENT not found");
        var key = Environment.GetEnvironmentVariable("OPENAI_TOKEN") 
            ?? throw new ArgumentException("env OPENAI_TOKEN not found");
        
        return ActivatorUtilities.CreateInstance<AzureEmbedService>(sp, new OpenAIClient(key), model);
    });

    services.AddHostedService<Worker>();
});

builder.ConfigureFunctionsWorkerDefaults();

var host = builder.Build();
host.Run();