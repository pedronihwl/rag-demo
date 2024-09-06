using Microsoft.Azure.Functions.Worker;
using Shared.Collections;
using Shared.Extensions;

namespace EmbedFunction;

public sealed class CosmosDbTriggerFunction(EmbedService service, ILoggerFactory factory)
{

    private readonly ILogger<CosmosDbTriggerFunction> _logger = factory.CreateLogger<CosmosDbTriggerFunction>();
    
    [Function("embed-file")]
    public async Task Run(
        [CosmosDBTrigger("simonaggio-docs", "db_files", CreateLeaseContainerIfNotExists = true)] 
        IReadOnlyList<FileCollection> input
        )
    {
        foreach (var file in input)
        {
            if (file.Status == FileCollection.FileStatus.NotProcessed)
            {
                await service.EmbedAsync(file);
            }
            else
            {
                _logger.LogInformation("The file has already been processed. id: {id}, name: {name}, context: {context}", file.Id, file.Name, file.Context);
            }
        }
    }
}