using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;

using Shared.Collections;
using Shared.Options;

namespace Backend.Services;

public class AzureContextService(
    BlobContainerClient container, 
    CosmosClient cosmos,
    IOptions<AzureContextOptions> options)
{
    private AzureContextOptions Options { get; } = options.Value;
    private Container GetContainer(string name)
    {
        Database db = cosmos.GetDatabase(Options.DbCollection);
        Container collection = db.GetContainer(name);

        return collection;
    }
    
    private async ValueTask<IEnumerable<TItem>> IterateAsync<TItem>(IQueryable<TItem> queryable, CancellationToken cancellationToken = default)
    {
        using var iterator = queryable.ToFeedIterator();

        List<TItem> results = [];

        while (iterator.HasMoreResults)
        {
            FeedResponse<TItem> feedResponse = await iterator
                .ReadNextAsync(cancellationToken)
                .ConfigureAwait(false);
            
            foreach (TItem result in feedResponse.Resource)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private async Task<bool> DeleteFragmentsAsync(string fileId, string context, CancellationToken cancelToken)
    {
        var dbFragments = GetContainer(Options.DbFragments);
        
        IQueryable<Fragment> query =
            dbFragments.GetItemLinqQueryable<Fragment>()
                .Where(x => x.Context == context && x.File == fileId);

        var fragments = (await IterateAsync(query, cancelToken)).ToList();
        
        if (fragments.Count == 0)
        {
            return true;
        }
        
        TransactionalBatch batch = dbFragments.CreateTransactionalBatch(new PartitionKey(context));
        
        foreach (var fragment in fragments)
        {
            batch.DeleteItem(fragment.Id);
        }
        
        using TransactionalBatchResponse response = await batch.ExecuteAsync(cancelToken);

        return response.IsSuccessStatusCode;
    }

    public async Task DeleteFile(string id, string context, CancellationToken cancelToken)
    {
        var dbContext = GetContainer(Options.DbContextName);

        ItemResponse<ContextCollection> ctxResponse =
            await dbContext.ReadItemAsync<ContextCollection>(context, new PartitionKey(context), null, cancelToken)
                .ConfigureAwait(false);

        var ctx = ctxResponse.Resource;
        
        if (ctx == null)
        {
            throw new ArgumentException("Context not found");
        }
        
        var dbFiles = GetContainer(Options.DbFileName);
        
        ItemResponse<FileCollection> response =
            await dbFiles.ReadItemAsync<FileCollection>(id, new PartitionKey(context), null, cancelToken)
                .ConfigureAwait(false);
        
        var file = response.Resource;

        if (file == null)
        {
            throw new ArgumentException("File not found");
        }
        
        bool deleted = await DeleteFragmentsAsync(file.Id, context, cancelToken);

        if (!deleted)
        {
            throw new ArgumentException("Failed deleting fragments");
        }

        string blob = $"{context}/{file.Hash}{Path.GetExtension(file.Name)}";
        var blobClient = container.GetBlobClient(blob);

        if (await blobClient.ExistsAsync(cancelToken))
        {
            await blobClient.DeleteAsync(cancellationToken: cancelToken);
        }
        else
        {
            throw new InvalidOperationException($"Blob '{blob}' not found");
        }
        
        _ = await dbFiles.DeleteItemAsync<FileCollection>(file.Id, new PartitionKey(file.Context), null, cancelToken)
            .ConfigureAwait(false);

        ctx.Files = ctx.Files.Where(y => y != id).ToArray();
        await dbContext.UpsertItemAsync(ctx, new PartitionKey(ctx.Id), null, cancelToken);
    }
    
    public async Task<object> GetContext(string id, string filePath, CancellationToken cancelToken)
    {
        var dbContext = GetContainer(Options.DbContextName);
        var dbFiles = GetContainer(Options.DbFileName);
        
        ItemResponse<ContextCollection> response =
            await dbContext.ReadItemAsync<ContextCollection>(id, new PartitionKey(id), null, cancelToken)
                .ConfigureAwait(false);

        var ctx = response.Resource;

        if (ctx == null)
        {
            throw new ArgumentException("Entity not found: " + id);
        }
        
        IQueryable<FileCollection> query =
            dbFiles.GetItemLinqQueryable<FileCollection>()
                .Where(x => ctx.Files.Contains(x.Id));

        var files = await IterateAsync(query, cancelToken);

        return new
        {
            ctx.Id,
            ctx.CreatedAt,
            files = files.Select(file => new
            {
                file.Id,
                file.Name,
                file.Pages,
                file.ProcessedPages,
                file.Status,
                file.Chunks,
                url = string.Format(filePath, file.Hash)
            })
        };
    }

    private async Task<FileCollection> IngestFileAsync(TransactionalBatch batch, IFormFile file, string context)
    {
        string extension = Path.GetExtension(file.FileName).ToLower();
        string hash = $"{Guid.NewGuid():N}";
        
        BlobClient blobClient = container.GetBlobClient($"{context}/{hash}{extension}");
        var entity = new FileCollection(context, hash)
        {
            Name = file.FileName
        };

        await using var stream = file.OpenReadStream();

        await blobClient.UploadAsync(stream, new BlobHttpHeaders()
        {
            ContentDisposition = file.ContentDisposition,
            ContentType = file.ContentType
        }, new Dictionary<string, string>()
        {
            { "fileId", entity.Id },
            { "contextId", context }
        });

        batch.CreateItem(entity);
        return entity;
    }

    public async Task<ContextCollection> AddFileAsync(string context, IFormFileCollection files, CancellationToken cancelToken)
    {
        var dbContext = GetContainer(Options.DbContextName);

        ItemResponse<ContextCollection> response =
            await dbContext.ReadItemAsync<ContextCollection>(context, new PartitionKey(context), null, cancelToken)
                .ConfigureAwait(false);

        var ctx = response.Resource;
        
        var entity = await IngestFilesAsync(files, context, cancelToken);

        var aux = entity.ToList().Select(item => item.Id).ToArray();
        ctx.Files = ctx.Files.Concat(aux).ToArray();

        await dbContext.UpsertItemAsync(ctx, new PartitionKey(ctx.Id), null, cancelToken);

        return ctx;
    }

    private async Task<IEnumerable<FileCollection>> IngestFilesAsync(
        IFormFileCollection files,
        ContextCollection ctx,
        CancellationToken cancelToken) => await IngestFilesAsync(files, ctx.Id, cancelToken);

    private async Task<IEnumerable<FileCollection>> IngestFilesAsync(
        IFormFileCollection files, 
        string ctx,
        CancellationToken cancelToken)
    {
        var dbFiles = GetContainer(Options.DbFileName);

        TransactionalBatch batch = dbFiles.CreateTransactionalBatch(new PartitionKey(ctx));

        var tasks = new Task<FileCollection>[files.Count];
        
        for (var i = 0; i < files.Count; i++)
        {
            tasks[i] = IngestFileAsync(batch, files[i], ctx);
        }

        try
        {
            cancelToken.ThrowIfCancellationRequested();

            var entities = await Task.WhenAll(tasks);

            TransactionalBatchResponse response = await batch.ExecuteAsync(cancelToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Batch update failed");
            }

            return entities;
        }
        catch (Exception)
        {
            var blobs = container.GetBlobsByHierarchy(delimiter: "/", prefix: ctx + "/", cancellationToken: CancellationToken.None);

            foreach (var item in blobs)
            {
                await container.DeleteBlobIfExistsAsync(item.Blob.Name, cancellationToken: CancellationToken.None);
            }

            throw;
        }
    }
    
    public async Task<ContextCollection> PostContextAsync(IFormFileCollection files, CancellationToken cancelToken)
    {
        var ctx = new ContextCollection();
        var dbCtx = GetContainer(Options.DbContextName);

        await dbCtx.CreateItemAsync(ctx, new PartitionKey(ctx.Id),null, cancelToken);

        var entitiesFiles = await IngestFilesAsync(files, ctx, cancelToken);

        ctx.Files = entitiesFiles.Select(item => item.Id).ToArray();
        await dbCtx.UpsertItemAsync(ctx, new PartitionKey(ctx.Id), null, cancelToken);

        return ctx;
    }
}