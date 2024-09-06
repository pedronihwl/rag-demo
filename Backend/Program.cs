using System.Diagnostics.Tracing;
using System.Text.Json;
using System.Text.Json.Serialization;
using AspNetCore.Proxy;
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

services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; 
    options.Cookie.Name = "antiforgery";
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

services.AddHttpClient();
services.AddRazorPages();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
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
    var config = sp.GetRequiredService<IConfiguration>();
        
    string embeddingModel = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_DEPLOYMENT") 
                            ?? throw new ArgumentException("env OPENAI_EMBEDDING_DEPLOYMENT not found");
        
    string key = config["openaiKey"]
                 ?? throw new ArgumentException("env OPENAI_TOKEN not found");
    
    var chat = Environment.GetEnvironmentVariable("OPENAI_CHAT_DEPLOYMENT") ?? throw new ArgumentException("env OPENAI_CHAT_DEPLOYMENT not found");
    
    return ActivatorUtilities.CreateInstance<AzureContextService>(sp, new OpenAIClient(key), embeddingModel, chat);
});


var app = builder.Build();

if (env.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

if (env.IsDevelopment())
{
    var spaDevServer = app.Configuration.GetValue<string>("spaDevServerUrl");
    if (!string.IsNullOrEmpty(spaDevServer))
    {
        app.MapWhen(
            context => { 
                var path = context.Request.Path.ToString();
                var isFileRequest = path.StartsWith("/@", StringComparison.InvariantCulture)
                                    || path.StartsWith("/src", StringComparison.InvariantCulture) 
                                    || path.StartsWith("/node_modules", StringComparison.InvariantCulture); 

                return isFileRequest;
            }, appBuilder => appBuilder.Run(context =>
            {
                var targetPath = $"{spaDevServer}{context.Request.Path}{context.Request.QueryString}";
                return context.HttpProxyAsync(targetPath);
            }));

    }
}

app.UseRouting();
app.UseStaticFiles();
app.UseCors();

app.MapRazorPages();

app.AddApi();

app.MapFallbackToPage("/_Host");
app.Run();