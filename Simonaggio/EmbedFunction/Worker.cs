using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Shared.Collections;
using Shared.Options;

namespace EmbedFunction;

public class Worker(
    ILogger<Worker> logger,
    IOptions<CosmosDbOptions> options,
    CosmosClient client,
    EmbedService service) : BackgroundService
{
    private readonly CosmosDbOptions _options = options.Value;
    
    protected override async Task ExecuteAsync(CancellationToken cancel)
    {
        await Task.Delay(2000, cancel);
        
        _ = Task.Run(() => ReadConsoleInput(cancel), cancel);

        while (!cancel.IsCancellationRequested)
        {
            await Task.Delay(1000, cancel);
        }
    }
    
    private async Task ReadConsoleInput(CancellationToken cancel)
    {
        var dbFiles = client.GetContainer(_options.DbCollection, _options.DbFileName);
        
        while (!cancel.IsCancellationRequested)
        {
            logger.LogInformation("Insert the file id and context id to process (ex: `fileId:contextId`): ");
            var input = await Task.Run(Console.ReadLine, cancel);

            var values = input?.Split(":") ?? [];

            if (values.Length < 2)
            {
                logger.LogWarning("Incorrect format");
                continue;
            }

            FileCollection? ctx = null;
            
            try
            {
                ItemResponse<FileCollection> response =
                    await dbFiles.ReadItemAsync<FileCollection>(values[0], new PartitionKey(values[1]), null, cancel)
                        .ConfigureAwait(false);

                ctx = response.Resource;
            }
            catch (Exception)
            {
                logger.LogWarning("Entity not found: {message}",string.Join(":", values));
                continue;
                
            }

            await service.EmbedAsync(ctx);
            
            logger.LogInformation("Processed success! type C to clear");
            
            var onClear = await Task.Run(Console.ReadLine, cancel);

            if (onClear == "C")
            {
                Console.Clear();
            }
        }
    }
}