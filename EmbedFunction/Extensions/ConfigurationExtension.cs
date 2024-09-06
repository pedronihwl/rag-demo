using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Shared.Options;
using Shared.Serializer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
namespace EmbedFunction.Extensions;

internal static class ConfigurationExtension
{
    internal static IServiceCollection AddCosmos(this IServiceCollection services)
    {
        static string GetRequiredEnvironmentVariable(string variableName)
        {
            return Environment.GetEnvironmentVariable(variableName) 
                   ?? throw new ArgumentException($"Environment variable '{variableName}' not found");
        }

        services.AddOptions<CosmosDbOptions>().Configure(opt =>
        {
            opt.DbCollection  = GetRequiredEnvironmentVariable("COSMOSDB_COLLECTION");
            opt.DbContextName = GetRequiredEnvironmentVariable("COSMOSDB_CONTEXTS");
            opt.DbFileName    = GetRequiredEnvironmentVariable("COSMOSDB_FILES");
            opt.DbFragments   = GetRequiredEnvironmentVariable("COSMOSDB_FRAGMENTS");
        });
        
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

        return services;
    }
}