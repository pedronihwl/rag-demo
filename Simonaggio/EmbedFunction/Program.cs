using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using EmbedFunction;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Azure;
using OpenAI;
using Shared.Extensions;
using Shared.Options;
using Shared.Serializer;
using Shared.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.ConfigureAzureKeyVault(new DefaultAzureCredential());

builder.Services.AddLogging();

builder.Services.AddAzureClients(opt =>
{
    opt.AddDocumentAnalysisClient(new Uri("https://cog-formreconizer.cognitiveservices.azure.com/"), new AzureKeyCredential("846e4b12958f4af9b795189a1dd1934c"));
});

builder.Services.AddOptions<CosmosDbOptions>()
    .Configure<IConfiguration>(
        (settings, configuration) => 
            configuration.GetSection("azureServiceOptions").Bind(settings));

builder.Services.AddSingleton<EmbedService>();

builder.Services.AddSingleton<CosmosClient>(prov =>
{
    var config = prov.GetRequiredService<IConfiguration>();

    var azureCosmosDbEndpoint = config["azureCosmosEndpoint"];
    ArgumentException.ThrowIfNullOrEmpty(azureCosmosDbEndpoint);
    
    var azureCosmosDbToken = config["azureCosmosToken"];
    ArgumentException.ThrowIfNullOrEmpty(azureCosmosDbEndpoint);
    
    CosmosClient client = new CosmosClientBuilder(azureCosmosDbEndpoint, azureCosmosDbToken)
        .WithCustomSerializer(new CosmosSystemTextJsonSerializer(new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        }))
        .Build();

    return client;
});

builder.Services.AddSingleton<BlobServiceClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    
    var azureStorageToken = config["stAccountToken"];
    ArgumentException.ThrowIfNullOrEmpty(azureStorageToken);

    var blobServiceClient = new BlobServiceClient(azureStorageToken);

    return blobServiceClient;
});

builder.Services.AddSingleton<BlobContainerClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var azureStorageContainer = config["stAccountContainer"];
    return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(azureStorageContainer);
});

builder.Services.AddSingleton<AzureEmbedService>(sp =>
{
    var model = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_DEPLOYMENT") ?? throw new ArgumentException("env OPENAI_EMBEDDING_DEPLOYMENT not found");
    var key = Environment.GetEnvironmentVariable("OPENAI_TOKEN") ?? throw new ArgumentException("env OPENAI_OPENAI_TOKEN not found");

    return ActivatorUtilities.CreateInstance<AzureEmbedService>(sp, new OpenAIClient(key), model);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();