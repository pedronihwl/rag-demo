using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Collections;
using Shared.Options;
using Shared.Services;

namespace EmbedFunction;

public class EmbedService(
    ILogger<EmbedService> logger,
    CosmosClient client,
    BlobContainerClient container,
    AzureEmbedService service,
    IOptions<CosmosDbOptions> options)
{

    private readonly CosmosDbOptions _options = options.Value;

    public async Task EmbedAsync(FileCollection file)
    {
        var db = client.GetDatabase(_options.DbCollection);
        var dbFragments = db.GetContainer(_options.DbFragments);
        var dbFiles = db.GetContainer(_options.DbFileName);

        string key = $"{file.Context}/{file.Hash}{Path.GetExtension(file.Name)}";
        BlobClient blob = container.GetBlobClient(key);

        BlobDownloadResult content = (await blob.DownloadContentAsync()).Value;

        service.ProgressChanged += async args =>
        {
            var actionFile = (FileCollection) args.UserState!;
            await dbFiles.UpsertItemAsync(actionFile, new PartitionKey(actionFile.Context));
        };

        var fragments = await service.EmbedPDFBlobAsync(content.Content.ToStream(), file);

        TransactionalBatch batch = dbFragments.CreateTransactionalBatch(new PartitionKey(file.Id));
        
        foreach (var fragment in fragments)
        {
            batch.CreateItem(fragment);
        }

        TransactionalBatchResponse response = await batch.ExecuteAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Batch update failed");
        }
    }
}