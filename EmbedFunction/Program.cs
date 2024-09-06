using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using EmbedFunction;
using EmbedFunction.Extensions;
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

builder.ConfigureAppConfiguration((_, config) =>
{
    config.ConfigureAzureKeyVault(credential);
});

builder.ConfigureServices((context, services) =>
{
    services.AddLogging();

    services.AddAzureClients(opt =>
    {
        opt.AddDocumentAnalysisClient(
            new Uri("https://cog-formreconizer.cognitiveservices.azure.com/"),
            new AzureKeyCredential("846e4b12958f4af9b795189a1dd1934c"));
    });
    
    services.AddSingleton<EmbedService>();

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
        var config = sp.GetRequiredService<IConfiguration>();
        
        string embeddingModel = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_DEPLOYMENT") 
            ?? throw new ArgumentException("env OPENAI_EMBEDDING_DEPLOYMENT not found");
        
        string key = config["openaiKey"]
            ?? throw new ArgumentException("env OPENAI_TOKEN not found");
        
        return ActivatorUtilities.CreateInstance<AzureEmbedService>(sp, new OpenAIClient(key), embeddingModel);
    });

    services.AddHostedService<Worker>();
    services.AddCosmos();
});

builder.ConfigureFunctionsWorkerDefaults(opt =>
{
    opt.Services.Configure<JsonSerializerOptions>(jsonSerializerOptions =>
    {
        jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        jsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        jsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
        jsonSerializerOptions.PropertyNameCaseInsensitive = true;
        jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
});

var host = builder.Build();
host.Run();