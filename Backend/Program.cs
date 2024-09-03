using System.Diagnostics.Tracing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core.Diagnostics;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Backend.Extensions;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using OpenAI;
using Shared.Extensions;
using Shared.Options;
using Shared.Serializer;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var env = builder.Environment;

using var listener = new AzureEventSourceListener(
    (e, message) => Console.WriteLine($"{e.EventSource.Name} [{e.Level}]: {message}"),
    level: EventLevel.Informational);

var credential = new DefaultAzureCredential();

builder.Configuration.ConfigureAzureKeyVault(credential);

services.AddCors(
    options =>
        options.AddDefaultPolicy(
            policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

services.AddLogging();
services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());

}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

services.AddSingleton<CosmosClient>(prov =>
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

services.AddSingleton<BlobServiceClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    
    var azureStorageToken = config["stAccountToken"];
    ArgumentException.ThrowIfNullOrEmpty(azureStorageToken);

    var blobServiceClient = new BlobServiceClient(azureStorageToken);

    return blobServiceClient;
});

services.AddSingleton<BlobContainerClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var azureStorageContainer = config["stAccountContainer"];
    return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(azureStorageContainer);
});

services.AddOptions<CosmosDbOptions>()
    .Configure<IConfiguration>(
        (settings, configuration) => 
            configuration.GetSection("azureServiceOptions").Bind(settings));

services.AddSingleton<AzureContextService>(sp =>
{
    var embedding = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_DEPLOYMENT") ?? throw new ArgumentException("env OPENAI_EMBEDDING_DEPLOYMENT not found");
    var key = Environment.GetEnvironmentVariable("OPENAI_TOKEN") ?? throw new ArgumentException("env OPENAI_OPENAI_TOKEN not found");
    var chat = Environment.GetEnvironmentVariable("OPENAI_CHAT_DEPLOYMENT") ?? throw new ArgumentException("env OPENAI_CHAT_DEPLOYMENT not found");
    
    return ActivatorUtilities.CreateInstance<AzureContextService>(sp, new OpenAIClient(key), embedding, chat);
});

var app = builder.Build();

app.UseRouting();
app.UseStaticFiles();
app.UseCors();

app.MapControllers();

app.AddApi();

app.Run();