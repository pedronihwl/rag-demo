using System.ComponentModel;
using System.Runtime.InteropServices.ComTypes;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Collections;
using Shared.Extensions;
using Shared.Options;
using Shared.Services;

namespace EmbedFunction;

public class EmbedService
{

    private readonly CosmosDbOptions _options;
    private readonly ILogger<EmbedService> _logger;
    private readonly Database _db;
    private readonly AzureEmbedService _service;
    private readonly BlobContainerClient _container;
    
    public EmbedService(ILogger<EmbedService> logger,
        CosmosClient client,
        BlobContainerClient container,
        AzureEmbedService service,
        IOptions<CosmosDbOptions> options)
    {

        _options = options.Value;
        _db = client.GetDatabase(_options.DbCollection).ReadAsync().Result;

        _logger = logger;
        _service = service;
        _container = container;
    }

    public async Task EmbedAsync(FileCollection file)
    {
        _logger.LogInformation("Starting embedding file {id},{name},{context}", file.Id, file.Name, file.Context);

        var dbFragments = _db.GetContainer(_options.DbFragments);
        var dbFiles = _db.GetContainer(_options.DbFileName);
        
        try
        {
            
            _service.ProgressChanged += Event;

            void Event(ProgressChangedEventArgs e)
            {
                var percent = e.ProgressPercentage;
                FileCollection collection = (FileCollection) e.UserState!;
                
                _logger.LogInformation("Progress: {percent}\n{file}", percent, collection.ToJsonString());
                dbFiles.UpsertItemAsync(collection, new PartitionKey(file.Context)).Wait();
            }
            // status - Processing
            
            file.Status = FileCollection.FileStatus.Processing;
            await dbFiles.UpsertItemAsync(file, new PartitionKey(file.Context));
            
            string key = $"{file.Context}/{file.Hash}{Path.GetExtension(file.Name)}";
            BlobClient blob = _container.GetBlobClient(key);

            BlobDownloadResult content = (await blob.DownloadContentAsync()).Value;
            
            var fragments = await _service.EmbedPDFBlobAsync(content.Content.ToStream(), file);

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

            file.Status = FileCollection.FileStatus.Processed;
            await dbFiles.UpsertItemAsync(file, new PartitionKey(file.Context));
            _logger.LogInformation("File processed with status success {id},{name},{context}", file.Id, file.Name, file.Context);
        }
        catch (Exception ex)
        {
            file.Status = FileCollection.FileStatus.ProcessingFailed;
            await dbFiles.UpsertItemAsync(file, new PartitionKey(file.Context));
            
            _logger.LogError("Failed processing file {id},{name},{context}. Cause:\n\t{exception}", file.Id, file.Name, file.Context, ex);
        }
    }
}