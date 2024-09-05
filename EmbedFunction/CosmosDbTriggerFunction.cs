using System.Reflection.Metadata;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Shared.Collections;

namespace EmbedFunction;

public static class CosmosDbTriggerFunction
{
    [Function("embed-file")]
    public static void Run(
        [CosmosDBTrigger(
            "simonaggio-docs",
            "db_files",
            Connection = "",
            LeaseContainerName = "db_leases",
            CreateLeaseContainerIfNotExists = true)] IReadOnlyList<FileCollection> input,
        ILogger log)
    {
        if (input != null && input.Count > 0)
        {
            foreach (var document in input)
            {
                log.LogInformation($"Documento inserido no Cosmos DB com ID: {document.Id}");
            }
        }
    }
}