using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Shared;
using Shared.Collections;
using Shared.Options;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace Backend.Services;

public class AzureContextService(
    BlobContainerClient container, 
    CosmosClient cosmos,
    OpenAIClient openAiClient,
    IOptions<CosmosDbOptions> options,
    string embeddingModel,
    string chatModel)
{
    private CosmosDbOptions Options { get; } = options.Value;
    private Container GetContainer(string name)
    {
        Database db = cosmos.GetDatabase(Options.DbCollection);
        Container collection = db.GetContainer(name);

        return collection;
    }
    
   public async Task<object?> ChatAsync(ChatRequest request, string context, CancellationToken cancel)
    {
        string question = request.LastUserQuestion ?? throw new InvalidOperationException("Use question is null");

        JsonArray? fragments = null;

        List<ChatMessage> messages = [];
        
        if (request.Overrides.RetrievalMode == RetrievalMode.RAG)
        {
            fragments = await CosmosSemanticSearch(context, cancel, question);

            string systemPrompt = """
                            Você é uma assistente especializada em auxiliar contadores, economistas e advogados na busca de informações específicas em fragmentos de textos fornecidos. Sua principal função é receber perguntas relacionadas a pontos específicos presentes nesses fragmentos e localizar as informações relevantes de forma precisa e detalhada. Ao responder, você deve:

                            1. Buscar e identificar informações precisas: Encontre as informações exatas que respondem à pergunta dentro dos fragmentos de texto fornecidos.
                            2. Fornecer contexto relevante: Além da informação solicitada, identifique e resuma qualquer contexto adicional que seja relevante para melhor compreensão do ponto em questão.
                            3. Evitar informações fictícias: Assegure-se de que todas as respostas sejam baseadas exclusivamente nos fragmentos de texto fornecidos, sem criar ou adicionar dados não verificados.

                            O objetivo é fornecer respostas claras, detalhadas e contextualmente informadas para cada consulta, facilitando a tomada de decisões e a compreensão dos temas abordados pelos usuários.
                            """;
            
            string assistantPrompt = """
                                     Os fragmentos de texto seguem o formato abaixo:

                                     ## Fragment {pageNumber}-{fileId}-{id}
                                     ... content
                                     ## END FRAGMENT

                                     - **Fragment {pageNumber}-{id}**: Indica o início de um fragmento de texto. O `{pageNumber}` é o número da página onde o fragmento foi encontrado e `{id}` é um identificador único para o fragmento.
                                     - **... content**: Representa o conteúdo do fragmento de texto. Este é o texto que será analisado para responder às perguntas.
                                     - **## END FRAGMENT**: Indica o final do fragmento de texto.

                                     Cada fragmento é delimitado por essas marcações, facilitando a identificação e análise dos conteúdos fornecidos.
                                     """;
            
            messages.Add(new SystemChatMessage(systemPrompt));
            messages.Add(new AssistantChatMessage(assistantPrompt));

            if (fragments.Count == 0)
            {
                messages.Add(new UserChatMessage("## Fragment \n nenhum fragmento encontrado \n## END FRAGMENT"));
                
            }
            else
            {
                string prompt = string.Empty;
                foreach (var fragment in fragments)
                {
                    prompt += $"""
                              ## Fragment {fragment?["index"]}-{fragment?["file"]}-{fragment?.GetElementIndex()}
                              {fragment?["text"]}
                              ## END FRAGMENT
                              """;
                }
                
                messages.Add(new UserChatMessage(prompt));
            }
        }
        else
        {
            string systemPrompt = """
                                  Você é uma assistente especializada em auxiliar contadores, economistas e advogados com suas dúvidasa respeito do conteúdo inserido.
                                  O objetivo é fornecer respostas claras, detalhadas e contextualmente informadas para pergunta, facilitando a tomada de decisões e a compreensão dos temas abordados pelos usuários.
                                  """;
            
            messages.Add(new SystemChatMessage(systemPrompt));
        }
        
        messages.Add(new UserChatMessage(question));
        ChatClient chat = openAiClient.GetChatClient(chatModel);

        ChatCompletionOptions options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                name: "answer_format",
                jsonSchema: BinaryData.FromString("""
                                                  {
                                                     "type":"object",
                                                     "properties":{
                                                        "answer":{
                                                           "type":"string",
                                                           "description":"A resposta da pergunta"
                                                        },
                                                        "fonts":{
                                                           "type":"array",
                                                           "items":{
                                                              "type":"string",
                                                              "description":"O identificador do fragmento no formato {pageNumber}-{fileId}-{id}"
                                                           },
                                                           "description":"Os fragmentos onde a informação foi localizada"
                                                        }
                                                     },
                                                     "required":[
                                                        "answer",
                                                        "fonts"
                                                     ],
                                                     "additionalProperties":false
                                                  }
                                                  """),
                strictSchemaEnabled: true)
        };
        
        var answer = await chat.CompleteChatAsync(messages, options, cancel);

        var stringAnswer = answer.Value.Content[0].Text;

        var node = JsonSerializer.Deserialize<JsonObject>(stringAnswer, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });

        var aux = fragments?
            .Select(n => $"{n?["file"]}").Where(x => !string.IsNullOrEmpty(x)).Distinct()
            .Select(x => JsonSerializer.SerializeToNode(x))
            .ToArray() ?? [];
        
        node?.Add("files", new JsonArray(aux));
        
        return node;
    }


    private record FragmentRecord(int Page, string File, int index);


    private static int[] ExpandArray(int[] pages, int max)
    {
        HashSet<int> result = new HashSet<int>(); 

        foreach (int page in pages)
        {
            if (page == 0)
            {
                result.Add(0);
                result.Add(1);
            }
            else if (page >= max)
            {
                result.Add(page - 1);
                result.Add(page);
            }
            else
            {
                result.Add(page - 1);
                result.Add(page);
                result.Add(page + 1);
            }
        }

        return result.OrderBy(x => x).ToArray(); // Retornando o array ordenado
    }
    
    public async Task<MemoryStream> DownloadFragmentAsync(string[] fonts, string context, CancellationToken cancelToken)
    {
        string pattern = @"^\d-file_\w{8}-\d$";
        Regex regex = new Regex(pattern);
        
        var aux = new List<FragmentRecord>();
        foreach (var font in fonts)
        {
            // {pageNumber}-{fileId}-{fragmentId}
            if (!regex.IsMatch(font))
            {
                throw new ArgumentException("Invalid format for font: " + font);
            }
            
            string[] values = font.Split("-");

            if (values.Length != 3)
            {
                throw new ArgumentException("Invalid format for font: " + font);
            }

            aux.Add(new FragmentRecord(int.Parse(values[0]), values[1], int.Parse(values[2])));
        }

        var filesIds = aux.Select(item => item.File).ToArray();
        var dbFiles = GetContainer(Options.DbFileName);

        var ids = filesIds;
        IQueryable<FileCollection> query =
            dbFiles.GetItemLinqQueryable<FileCollection>()
                .Where(x => ids.Contains(x.Id) && x.Context == context);
        
        var files = (await IterateAsync(query, cancelToken)).ToList();

        filesIds = files.Select(x => x.Id).ToArray();
        
        var blobs = container
            .GetBlobsByHierarchy(delimiter: "/", prefix: context + "/",traits: BlobTraits.Metadata, cancellationToken: CancellationToken.None)
            .ToList();

        blobs = blobs.Where(blob => filesIds.Contains(blob.Blob.Metadata["fileId"])).ToList();
        
        PdfDocument pagePdf = new PdfDocument();
        
        foreach (var blob in blobs)
        {
            int[] pages = aux.Where(x => x.File == blob.Blob.Metadata["fileId"]).Select(x => x.Page).Distinct().ToArray();
            
            using (var ms = new MemoryStream())
            {
                await container.GetBlobClient(blob.Blob.Name).DownloadToAsync(ms, cancelToken);
                ms.Position = 0;

                using (var pdfDocument = PdfReader.Open(ms, PdfDocumentOpenMode.Import))
                {
                    pages = ExpandArray(pages, pdfDocument.Pages.Count - 1);

                    for (var i = 0; i < pages.Length; i++)
                    {
                        var doc = pdfDocument.Pages[pages[i]];
                        pagePdf.AddPage(doc);
                    }
                }
            }
        }
        
        var memoryStream = new MemoryStream();
        pagePdf.Save(memoryStream, false); 
        memoryStream.Position = 0;

        return memoryStream;
    }

    private async Task<JsonArray> CosmosSemanticSearch(string context, CancellationToken cancel, string question)
    {
        var embeddingClient = openAiClient.GetEmbeddingClient(embeddingModel);

        var embedQuestion = (await embeddingClient.GenerateEmbeddingAsync(question, cancellationToken: cancel)).Value.Vector;
            
        var dbFragments = GetContainer(Options.DbFragments);

        var queryDefinition = new QueryDefinition("SELECT TOP 3 c.text, c.index, c.file, VectorDistance(c.embeddings, @embedQuestion) AS score FROM c WHERE c.context = @context ORDER BY VectorDistance(c.embeddings, @embedQuestion)")
            .WithParameter("@context", context)
            .WithParameter("@embedQuestion", embedQuestion);

        using FeedIterator<JsonNode> queryIterator = dbFragments.GetItemQueryIterator<JsonNode>(queryDefinition);

        JsonArray array = new JsonArray();

        while (queryIterator.HasMoreResults)
        {
            FeedResponse<JsonNode> response = await queryIterator.ReadNextAsync(cancel).ConfigureAwait(false);
            foreach (JsonNode item in response)
            {
                array.Add(item);
            }
        }

        return array;
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
        
        TransactionalBatch batch = dbFragments.CreateTransactionalBatch(new PartitionKey(fileId));
        
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
        
        _ = await dbFiles.DeleteItemAsync<FileCollection>(file.Id, new PartitionKey(file.Context), null, cancelToken).ConfigureAwait(false);

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